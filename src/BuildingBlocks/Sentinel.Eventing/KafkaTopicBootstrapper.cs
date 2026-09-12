using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sentinel.Contracts;

namespace Sentinel.Eventing;

public sealed class KafkaTopicBootstrapper : IHostedService
{
    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaTopicBootstrapper> _logger;

    public KafkaTopicBootstrapper(IOptions<KafkaOptions> options, ILogger<KafkaTopicBootstrapper> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.AutoCreateTopics)
        {
            return;
        }

        using var admin = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = _options.BootstrapServers
        }).Build();

        var specifications = KafkaTopics.All.Select(topic => new TopicSpecification
        {
            Name = topic,
            NumPartitions = _options.TopicPartitions,
            ReplicationFactor = _options.TopicReplicationFactor
        }).ToList();

        try
        {
            await admin.CreateTopicsAsync(specifications).ConfigureAwait(false);
            _logger.LogInformation("Ensured Kafka topics exist");
        }
        catch (CreateTopicsException ex) when (ex.Results.All(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
        {
            _logger.LogDebug("Kafka topics already exist");
        }
        catch (CreateTopicsException ex)
        {
            var unexpected = ex.Results.Where(r => r.Error.Code != ErrorCode.TopicAlreadyExists).ToList();
            if (unexpected.Count > 0)
            {
                _logger.LogWarning(ex, "Some Kafka topics could not be created");
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
