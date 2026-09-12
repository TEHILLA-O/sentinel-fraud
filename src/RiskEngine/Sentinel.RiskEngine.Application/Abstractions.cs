using Sentinel.Contracts;
using Sentinel.Contracts.Events;
using Sentinel.RiskEngine.Domain.Models;
using Sentinel.RiskEngine.Domain.Rules;
using Sentinel.RiskEngine.Domain.Scoring;
using Sentinel.SharedKernel;

namespace Sentinel.RiskEngine.Application;

public interface IVelocityStore
{
    Task<VelocitySnapshot> RecordAndSnapshotAsync(
        TransactionSnapshot transaction,
        bool failed,
        CancellationToken cancellationToken);
}

public interface ICustomerProfileStore
{
    Task<CustomerBehaviourProfile> GetAsync(string accountId, string customerId, string currency, CancellationToken cancellationToken);

    Task<IReadOnlyList<KnownDeviceSnapshot>> GetDevicesAsync(string customerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<HistoricalTransaction>> GetRecentTransactionsAsync(
        string accountId,
        int take,
        CancellationToken cancellationToken);

    Task ApplyTransactionAsync(TransactionSnapshot transaction, RiskDecision decision, CancellationToken cancellationToken);
}

public interface IRuleConfigurationProvider
{
    Task<ActiveRuleset> GetActiveAsync(CancellationToken cancellationToken);
}

public sealed record ActiveRuleset(
    string Version,
    IReadOnlyDictionary<string, RuleParameterSet> Parameters);

public interface IRiskDecisionRepository
{
    Task<bool> ExistsForTransactionAsync(string transactionId, CancellationToken cancellationToken);

    Task SaveAsync(PersistedDecision decision, CancellationToken cancellationToken);

    Task<PersistedDecision?> GetByTransactionIdAsync(string transactionId, CancellationToken cancellationToken);
}

public sealed record PersistedDecision
{
    public required Guid Id { get; init; }

    public required TransactionSnapshot Transaction { get; init; }

    public required RiskEvaluation Evaluation { get; init; }

    public required string CorrelationId { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required string ProfileSnapshotJson { get; init; }
}

public interface IInboxStore
{
    Task<bool> TryBeginAsync(Guid eventId, string consumer, CancellationToken cancellationToken);

    Task CompleteAsync(Guid eventId, CancellationToken cancellationToken);
}

public interface IOutboxStore
{
    Task EnqueueAsync(string topic, string key, string payload, string eventType, CancellationToken cancellationToken);

    Task<IReadOnlyList<OutboxRecord>> DequeueBatchAsync(int take, CancellationToken cancellationToken);

    Task MarkPublishedAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken);
}

public sealed record OutboxRecord(
    Guid Id,
    string Topic,
    string Key,
    string Payload,
    string EventType,
    DateTimeOffset CreatedAt);

public interface ITransactionValidator
{
    Result<TransactionSnapshot> Validate(TransactionReceivedV1 message);
}

public sealed class TransactionValidator : ITransactionValidator
{
    public Result<TransactionSnapshot> Validate(TransactionReceivedV1 message)
    {
        if (string.IsNullOrWhiteSpace(message.TransactionId))
        {
            return Result.Failure<TransactionSnapshot>(Error.Validation("TransactionId is required."));
        }

        if (string.IsNullOrWhiteSpace(message.AccountId))
        {
            return Result.Failure<TransactionSnapshot>(Error.Validation("AccountId is required."));
        }

        if (string.IsNullOrWhiteSpace(message.CustomerId))
        {
            return Result.Failure<TransactionSnapshot>(Error.Validation("CustomerId is required."));
        }

        if (message.Amount <= 0)
        {
            return Result.Failure<TransactionSnapshot>(Error.Validation("Amount must be greater than zero."));
        }

        if (string.IsNullOrWhiteSpace(message.Currency) || message.Currency.Length != 3)
        {
            return Result.Failure<TransactionSnapshot>(Error.Validation("Currency must be a 3-letter ISO code."));
        }

        if (string.IsNullOrWhiteSpace(message.Country) || message.Country.Length != 2)
        {
            return Result.Failure<TransactionSnapshot>(Error.Validation("Country must be a 2-letter ISO code."));
        }

        if (string.IsNullOrWhiteSpace(message.MerchantId))
        {
            return Result.Failure<TransactionSnapshot>(Error.Validation("MerchantId is required."));
        }

        if (message.EventId == Guid.Empty)
        {
            return Result.Failure<TransactionSnapshot>(Error.Validation("EventId is required."));
        }

        return Result.Success(new TransactionSnapshot
        {
            TransactionId = message.TransactionId.Trim(),
            AccountId = message.AccountId.Trim(),
            CustomerId = message.CustomerId.Trim(),
            Amount = new Money(message.Amount, message.Currency),
            MerchantId = message.MerchantId.Trim(),
            MerchantCategory = message.MerchantCategory,
            Country = message.Country.Trim().ToUpperInvariant(),
            City = message.City,
            Timestamp = message.Timestamp.ToUniversalTime(),
            CardPresent = message.CardPresent,
            Channel = message.Channel,
            DeviceId = message.DeviceId,
            IpAddress = message.IpAddress,
            CorrelationId = string.IsNullOrWhiteSpace(message.CorrelationId)
                ? message.EventId.ToString("N")
                : message.CorrelationId
        });
    }
}
