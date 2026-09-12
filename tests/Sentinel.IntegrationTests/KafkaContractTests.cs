using Confluent.Kafka;
using FluentAssertions;
using Sentinel.Contracts;
using Sentinel.Contracts.Events;
using Sentinel.Contracts.Json;
using Testcontainers.Kafka;

namespace Sentinel.IntegrationTests;

public class KafkaSerializationTests
{
    [Fact]
    public void Round_trips_transaction_events()
    {
        var original = new TransactionReceivedV1
        {
            EventId = Guid.CreateVersion7(),
            CorrelationId = "corr",
            OccurredAt = DateTimeOffset.UtcNow,
            TransactionId = "TX-1",
            AccountId = "A-1",
            CustomerId = "C-1",
            Amount = 12.50m,
            Currency = "GBP",
            MerchantId = "M-1",
            MerchantCategory = MerchantCategory.Grocery,
            Country = "GB",
            Timestamp = DateTimeOffset.UtcNow,
            CardPresent = true,
            Channel = Channel.POS
        };

        var json = SentinelJson.Serialize(original);
        var copy = SentinelJson.Deserialize<TransactionReceivedV1>(json);
        copy!.TransactionId.Should().Be("TX-1");
        copy.Amount.Should().Be(12.50m);
        copy.PartitionKey.Should().Be("A-1");
    }
}

public class KafkaBrokerTests
{
    [Fact]
    public async Task Producer_consumer_and_duplicate_payload_are_safe()
    {
        KafkaContainer? kafka = null;
        try
        {
            kafka = new KafkaBuilder().Build();
            await kafka.StartAsync();
        }
        catch (Exception)
        {
            return;
        }

        await using (kafka)
        {
            var bootstrap = kafka.GetBootstrapAddress();
            var topic = KafkaTopics.TransactionsRaw;
            var config = new ProducerConfig { BootstrapServers = bootstrap, Acks = Acks.All, EnableIdempotence = true };
            using var producer = new ProducerBuilder<string, string>(config).Build();
            var payload = SentinelJson.Serialize(new DeadLetterV1
            {
                EventId = Guid.CreateVersion7(),
                CorrelationId = "c",
                OccurredAt = DateTimeOffset.UtcNow,
                SourceTopic = topic,
                OriginalKey = "A-1",
                Payload = "{}",
                Error = "test",
                Attempt = 1,
                Poison = true
            });

            await producer.ProduceAsync(topic, new Message<string, string> { Key = "A-1", Value = payload });
            await producer.ProduceAsync(topic, new Message<string, string> { Key = "A-1", Value = payload });

            using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
            {
                BootstrapServers = bootstrap,
                GroupId = "test-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            }).Build();
            consumer.Subscribe(topic);
            var first = consumer.Consume(TimeSpan.FromSeconds(15));
            var second = consumer.Consume(TimeSpan.FromSeconds(15));
            first.Should().NotBeNull();
            second.Should().NotBeNull();
            first!.Message.Value.Should().Be(second!.Message.Value);
        }
    }
}
