using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sentinel.SharedKernel;

namespace Sentinel.Eventing;

public sealed class KafkaEventPublisher : IEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaEventPublisher> _logger;

    public KafkaEventPublisher(IOptions<KafkaOptions> options, ILogger<KafkaEventPublisher> logger)
    {
        _logger = logger;
        var config = new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            ClientId = options.Value.ClientId,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageSendMaxRetries = options.Value.RetryCount,
            RetryBackoffMs = options.Value.RetryDelayMilliseconds,
            LingerMs = 5,
            CompressionType = CompressionType.Lz4
        };
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishAsync(
        string topic,
        string key,
        string payload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        using var activity = SentinelTelemetry.ActivitySource.StartActivity("sentinel.kafka.publish");
        activity?.SetTag("messaging.destination", topic);
        activity?.SetTag("correlation.id", correlationId);

        var message = new Message<string, string>
        {
            Key = key,
            Value = payload,
            Headers = new Headers
            {
                { "correlation-id", System.Text.Encoding.UTF8.GetBytes(correlationId) }
            }
        };

        try
        {
            var result = await _producer.ProduceAsync(topic, message, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug(
                "Published {Topic} partition={Partition} offset={Offset} key={Key}",
                topic,
                result.Partition.Value,
                result.Offset.Value,
                key);
        }
        catch (ProduceException<string, string> ex)
        {
            throw new TransientInfrastructureException($"Failed to publish to {topic}", ex);
        }
    }

    public void Dispose() => _producer.Dispose();
}
