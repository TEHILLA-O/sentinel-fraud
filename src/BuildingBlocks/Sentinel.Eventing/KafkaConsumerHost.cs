using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sentinel.Contracts;
using Sentinel.Contracts.Events;
using Sentinel.Contracts.Json;
using Sentinel.SharedKernel;

namespace Sentinel.Eventing;

public abstract class KafkaConsumerHost<TMessage> : BackgroundService
{
    private readonly IEventPublisher _publisher;
    private readonly KafkaOptions _options;
    private readonly ILogger _logger;

    protected KafkaConsumerHost(
        IOptions<KafkaOptions> options,
        IEventPublisher publisher,
        ILogger logger)
    {
        _options = options.Value;
        _publisher = publisher;
        _logger = logger;
    }

    protected abstract string Topic { get; }

    protected abstract string GroupId { get; }

    protected virtual int MaxAttempts => _options.RetryCount;

    protected abstract TMessage Deserialize(string payload);

    protected abstract Task HandleAsync(TMessage message, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = GroupId,
            ClientId = $"{_options.ClientId}-{GroupId}",
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnablePartitionEof = false,
            IsolationLevel = IsolationLevel.ReadCommitted
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(Topic);
        _logger.LogInformation("Consuming {Topic} as {Group}", Topic, GroupId);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result = null;
                try
                {
                    result = consumer.Consume(stoppingToken);
                    if (result?.Message?.Value is null)
                    {
                        continue;
                    }

                    await ProcessWithRetryAsync(result, stoppingToken).ConfigureAwait(false);
                    consumer.Commit(result);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Kafka consume failure on {Topic}", Topic);
                    SentinelTelemetry.ProcessingErrors.Add(1);
                }
            }
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task ProcessWithRetryAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                var message = Deserialize(result.Message.Value);
                await HandleAsync(message, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (PoisonMessageException ex)
            {
                await DeadLetterAsync(result, ex.Message, attempt, poison: true, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                var delay = TimeSpan.FromMilliseconds(_options.RetryDelayMilliseconds * Math.Pow(2, attempt - 1));
                _logger.LogWarning(
                    ex,
                    "Transient failure processing {Topic} key={Key} attempt={Attempt}",
                    Topic,
                    result.Message.Key,
                    attempt);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await DeadLetterAsync(result, ex.Message, attempt, poison: false, cancellationToken).ConfigureAwait(false);
                return;
            }
        }
    }

    private async Task DeadLetterAsync(
        ConsumeResult<string, string> result,
        string error,
        int attempt,
        bool poison,
        CancellationToken cancellationToken)
    {
        SentinelTelemetry.DeadLetter.Add(1);
        var dead = new DeadLetterV1
        {
            EventId = Guid.CreateVersion7(),
            CorrelationId = ReadCorrelation(result) ?? Guid.CreateVersion7().ToString("N"),
            OccurredAt = DateTimeOffset.UtcNow,
            SourceTopic = Topic,
            OriginalKey = result.Message.Key,
            Payload = result.Message.Value,
            Error = error,
            Attempt = attempt,
            Poison = poison
        };

        _logger.LogError(
            "Sending {Topic} key={Key} to dead letter. Poison={Poison} Error={Error}",
            Topic,
            result.Message.Key,
            poison,
            error);

        await _publisher.PublishAsync(
                KafkaTopics.DeadLetter,
                result.Message.Key ?? dead.EventId.ToString("N"),
                SentinelJson.Serialize(dead),
                dead.CorrelationId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static string? ReadCorrelation(ConsumeResult<string, string> result)
    {
        var header = result.Message.Headers?.FirstOrDefault(h => h.Key == "correlation-id");
        return header is null ? null : System.Text.Encoding.UTF8.GetString(header.GetValueBytes());
    }
}
