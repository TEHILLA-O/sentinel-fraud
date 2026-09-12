using System.Text.Json.Serialization;

namespace Sentinel.Contracts.Events;

public sealed record TransactionReceivedV1
{
    public required Guid EventId { get; init; }

    public required string CorrelationId { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public required string TransactionId { get; init; }

    public required string AccountId { get; init; }

    public required string CustomerId { get; init; }

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required string MerchantId { get; init; }

    public required MerchantCategory MerchantCategory { get; init; }

    public required string Country { get; init; }

    public string? City { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required bool CardPresent { get; init; }

    public required Channel Channel { get; init; }

    public string? DeviceId { get; init; }

    public string? IpAddress { get; init; }

    [JsonIgnore]
    public string PartitionKey => AccountId;
}

public sealed record TransactionValidatedV1
{
    public required Guid EventId { get; init; }

    public required string CorrelationId { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public required TransactionReceivedV1 Transaction { get; init; }
}

public sealed record RiskDecisionV1
{
    public required Guid EventId { get; init; }

    public required string CorrelationId { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public required string TransactionId { get; init; }

    public required string AccountId { get; init; }

    public required string CustomerId { get; init; }

    public required int RiskScore { get; init; }

    public required RiskLevel RiskLevel { get; init; }

    public required RiskDecision Decision { get; init; }

    public required IReadOnlyList<TriggeredRuleV1> TriggeredRules { get; init; }

    public required IReadOnlyList<string> Reasons { get; init; }

    public required IReadOnlyDictionary<string, string> Evidence { get; init; }

    public required long ProcessingTimeMs { get; init; }

    public required string ModelVersion { get; init; }

    public required string RulesetVersion { get; init; }

    public required TransactionReceivedV1 Transaction { get; init; }

    [JsonIgnore]
    public string PartitionKey => AccountId;
}

public sealed record TriggeredRuleV1
{
    public required string RuleCode { get; init; }

    public required int Score { get; init; }

    public required string Reason { get; init; }

    public required IReadOnlyDictionary<string, string> Evidence { get; init; }
}

public sealed record AlertRaisedV1
{
    public required Guid EventId { get; init; }

    public required string CorrelationId { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public required string TransactionId { get; init; }

    public required string AccountId { get; init; }

    public required string CustomerId { get; init; }

    public required RiskDecision Decision { get; init; }

    public required int RiskScore { get; init; }

    public required string Summary { get; init; }
}

public sealed record DeadLetterV1
{
    public required Guid EventId { get; init; }

    public required string CorrelationId { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public required string SourceTopic { get; init; }

    public required string? OriginalKey { get; init; }

    public required string Payload { get; init; }

    public required string Error { get; init; }

    public required int Attempt { get; init; }

    public required bool Poison { get; init; }
}

public sealed record ProfileUpdatedV1
{
    public required Guid EventId { get; init; }

    public required string CorrelationId { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public required string AccountId { get; init; }

    public required string CustomerId { get; init; }

    public required decimal AverageTransactionAmount { get; init; }

    public required string HomeCountry { get; init; }

    public required int TransactionCount { get; init; }
}

public sealed record CaseFeedbackV1
{
    public required Guid EventId { get; init; }

    public required string CorrelationId { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public required Guid CaseId { get; init; }

    public required string TransactionId { get; init; }

    public required FeedbackType FeedbackType { get; init; }

    public required string Analyst { get; init; }

    public string? Notes { get; init; }
}
