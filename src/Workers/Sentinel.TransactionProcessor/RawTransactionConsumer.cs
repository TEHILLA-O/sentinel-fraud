using Microsoft.Extensions.Options;
using Sentinel.Contracts;
using Sentinel.Contracts.Events;
using Sentinel.Contracts.Json;
using Sentinel.Eventing;
using Sentinel.RiskEngine.Application;
using Sentinel.SharedKernel;

namespace Sentinel.TransactionProcessor;

public sealed class RawTransactionConsumer : KafkaConsumerHost<TransactionReceivedV1>
{
    private readonly IServiceScopeFactory _scopes;

    public RawTransactionConsumer(
        IOptions<KafkaOptions> options,
        IEventPublisher publisher,
        ILogger<RawTransactionConsumer> logger,
        IServiceScopeFactory scopes)
        : base(options, publisher, logger)
    {
        _scopes = scopes;
    }

    protected override string Topic => KafkaTopics.TransactionsRaw;

    protected override string GroupId => ConsumerGroups.TransactionProcessor;

    protected override TransactionReceivedV1 Deserialize(string payload) =>
        SentinelJson.Deserialize<TransactionReceivedV1>(payload)
        ?? throw new PoisonMessageException("Transaction payload could not be deserialized.");

    protected override async Task HandleAsync(TransactionReceivedV1 message, CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<TransactionProcessingService>();
        await processor.ProcessAsync(message, cancellationToken).ConfigureAwait(false);
    }
}
