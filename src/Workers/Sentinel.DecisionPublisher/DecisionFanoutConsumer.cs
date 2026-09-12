using Microsoft.Extensions.Options;
using Sentinel.Contracts;
using Sentinel.Contracts.Events;
using Sentinel.Contracts.Json;
using Sentinel.Eventing;
using Sentinel.Persistence;
using Sentinel.RiskEngine.Application;
using Sentinel.SharedKernel;
using StackExchange.Redis;

namespace Sentinel.DecisionPublisher;

public sealed class DecisionFanoutConsumer : KafkaConsumerHost<RiskDecisionV1>
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IConnectionMultiplexer _redis;

    public DecisionFanoutConsumer(
        IOptions<KafkaOptions> options,
        IEventPublisher publisher,
        ILogger<DecisionFanoutConsumer> logger,
        IServiceScopeFactory scopes,
        IConnectionMultiplexer redis)
        : base(options, publisher, logger)
    {
        _scopes = scopes;
        _redis = redis;
    }

    protected override string Topic => KafkaTopics.RiskDecisions;

    protected override string GroupId => ConsumerGroups.DecisionPublisher;

    protected override RiskDecisionV1 Deserialize(string payload) =>
        SentinelJson.Deserialize<RiskDecisionV1>(payload)
        ?? throw new PoisonMessageException("Risk decision payload could not be deserialized.");

    protected override async Task HandleAsync(RiskDecisionV1 message, CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var inbox = scope.ServiceProvider.GetRequiredService<IInboxStore>();
        if (!await inbox.TryBeginAsync(message.EventId, GroupId, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        if (message.Decision is RiskDecision.Review or RiskDecision.Block)
        {
            var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
            if (!db.Cases.Any(c => c.TransactionId == message.TransactionId))
            {
                var now = DateTimeOffset.UtcNow;
                var caseId = Guid.CreateVersion7();
                db.Cases.Add(new FraudCaseRecord
                {
                    CaseId = caseId,
                    TransactionId = message.TransactionId,
                    CustomerId = message.CustomerId,
                    AccountId = message.AccountId,
                    RiskScore = message.RiskScore,
                    RiskLevel = message.RiskLevel,
                    Status = CaseStatus.Open,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                db.CaseHistory.Add(new CaseHistoryRecord
                {
                    Id = Guid.CreateVersion7(),
                    CaseId = caseId,
                    FromStatus = CaseStatus.Open,
                    ToStatus = CaseStatus.Open,
                    Actor = "system",
                    Notes = "Case opened from risk decision.",
                    ChangedAt = now
                });
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                SentinelTelemetry.CasesOpen.Add(1);
            }
        }

        await _redis.GetSubscriber().PublishAsync(
            RedisChannel.Literal(RedisChannels.LiveDecisions),
            SentinelJson.Serialize(message));

        if (message.Decision is RiskDecision.Review or RiskDecision.Block)
        {
            await _redis.GetSubscriber().PublishAsync(
                RedisChannel.Literal(RedisChannels.LiveAlerts),
                SentinelJson.Serialize(message));
        }

        await inbox.CompleteAsync(message.EventId, cancellationToken).ConfigureAwait(false);
    }
}
