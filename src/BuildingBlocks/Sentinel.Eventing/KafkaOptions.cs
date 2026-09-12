namespace Sentinel.Eventing;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "localhost:9092";

    public string ClientId { get; set; } = "sentinel";

    public int RetryCount { get; set; } = 3;

    public int RetryDelayMilliseconds { get; set; } = 250;

    public bool AutoCreateTopics { get; set; } = true;

    public int TopicPartitions { get; set; } = 6;

    public short TopicReplicationFactor { get; set; } = 1;
}
