using Microsoft.Extensions.Options;
using Sentinel.Contracts;
using Sentinel.Contracts.Events;
using Sentinel.Contracts.Json;
using Sentinel.Eventing;
using Sentinel.RiskEngine.Application;
using Sentinel.SharedKernel;

namespace Sentinel.ProfileProcessor;

public sealed class ProfileUpdateConsumer : KafkaConsumerHost<RiskDecisionV1>
{
    private readonly IServiceScopeFactory _scopes;

    public ProfileUpdateConsumer(
        IOptions<KafkaOptions> options,
        IEventPublisher publisher,
        ILogger<ProfileUpdateConsumer> logger,
        IServiceScopeFactory scopes)
        : base(options, publisher, logger)
    {
        _scopes = scopes;
    }

    protected override string Topic => KafkaTopics.RiskDecisions;

    protected override string GroupId => ConsumerGroups.ProfileProcessor;

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

        var profiles = scope.ServiceProvider.GetRequiredService<ICustomerProfileStore>();
        var validator = scope.ServiceProvider.GetRequiredService<ITransactionValidator>();
        var snapshot = validator.Validate(message.Transaction);
        if (snapshot.IsFailure)
        {
            throw new PoisonMessageException(snapshot.Error!.Message);
        }

        await profiles.ApplyTransactionAsync(snapshot.Value, message.Decision, cancellationToken).ConfigureAwait(false);

        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
        var updated = new ProfileUpdatedV1
        {
            EventId = Guid.CreateVersion7(),
            CorrelationId = message.CorrelationId,
            OccurredAt = clock.UtcNow,
            AccountId = message.AccountId,
            CustomerId = message.CustomerId,
            AverageTransactionAmount = snapshot.Value.Amount.Amount,
            HomeCountry = snapshot.Value.Country,
            TransactionCount = 1
        };
        await outbox.EnqueueAsync(
                KafkaTopics.ProfileUpdated,
                message.AccountId,
                SentinelJson.Serialize(updated),
                EventTypes.ProfileUpdated,
                cancellationToken)
            .ConfigureAwait(false);
        await inbox.CompleteAsync(message.EventId, cancellationToken).ConfigureAwait(false);
    }
}
