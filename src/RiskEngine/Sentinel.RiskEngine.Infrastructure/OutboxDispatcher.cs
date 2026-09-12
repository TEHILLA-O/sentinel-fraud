using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sentinel.Eventing;
using Sentinel.RiskEngine.Application;

namespace Sentinel.RiskEngine.Infrastructure;

public sealed class OutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IEventPublisher _publisher;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(
        IServiceScopeFactory scopes,
        IEventPublisher publisher,
        ILogger<OutboxDispatcher> logger)
    {
        _scopes = scopes;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var outbox = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
                var batch = await outbox.DequeueBatchAsync(50, stoppingToken).ConfigureAwait(false);
                if (batch.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250), stoppingToken).ConfigureAwait(false);
                    continue;
                }

                var published = new List<Guid>();
                foreach (var item in batch)
                {
                    await _publisher.PublishAsync(item.Topic, item.Key, item.Payload, item.Key, stoppingToken)
                        .ConfigureAwait(false);
                    published.Add(item.Id);
                }

                await outbox.MarkPublishedAsync(published, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Outbox dispatch failed; will retry");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
