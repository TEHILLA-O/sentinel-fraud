using Sentinel.Contracts;

namespace Sentinel.Persistence;

public sealed class TransactionRecord
{
    public required string TransactionId { get; set; }

    public required string AccountId { get; set; }

    public required string CustomerId { get; set; }

    public required decimal Amount { get; set; }

    public required string Currency { get; set; }

    public required string MerchantId { get; set; }

    public required MerchantCategory MerchantCategory { get; set; }

    public required string Country { get; set; }

    public string? City { get; set; }

    public required DateTimeOffset Timestamp { get; set; }

    public required bool CardPresent { get; set; }

    public required Channel Channel { get; set; }

    public string? DeviceId { get; set; }

    public string? IpAddress { get; set; }

    public required string CorrelationId { get; set; }

    public required DateTimeOffset IngestedAt { get; set; }
}

public sealed class RiskDecisionRecord
{
    public required Guid Id { get; set; }

    public required string TransactionId { get; set; }

    public required string AccountId { get; set; }

    public required string CustomerId { get; set; }

    public required int RawScore { get; set; }

    public required int RiskScore { get; set; }

    public required RiskLevel RiskLevel { get; set; }

    public required RiskDecision Decision { get; set; }

    public required string TriggeredRulesJson { get; set; }

    public required string ReasonsJson { get; set; }

    public required string EvidenceJson { get; set; }

    public required string TransactionSnapshotJson { get; set; }

    public required string ProfileSnapshotJson { get; set; }

    public required string RuleConfigurationJson { get; set; }

    public required string RulesetVersion { get; set; }

    public required string ModelVersion { get; set; }

    public required long ProcessingTimeMs { get; set; }

    public required string CorrelationId { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }
}

public sealed class RulesetVersionRecord
{
    public required string Version { get; set; }

    public required bool IsActive { get; set; }

    public required string ConfigurationJson { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }

    public string? CreatedBy { get; set; }
}

public sealed class RiskRuleConfigurationRecord
{
    public required Guid Id { get; set; }

    public required string RulesetVersion { get; set; }

    public required string RuleCode { get; set; }

    public required bool Enabled { get; set; }

    public required int Score { get; set; }

    public required string ParametersJson { get; set; }
}

public sealed class FraudCaseRecord
{
    public required Guid CaseId { get; set; }

    public required string TransactionId { get; set; }

    public required string CustomerId { get; set; }

    public required string AccountId { get; set; }

    public required int RiskScore { get; set; }

    public required RiskLevel RiskLevel { get; set; }

    public required CaseStatus Status { get; set; }

    public string? AssignedAnalyst { get; set; }

    public RiskDecision? AnalystDecision { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }

    public required DateTimeOffset UpdatedAt { get; set; }
}

public sealed class CaseHistoryRecord
{
    public required Guid Id { get; set; }

    public required Guid CaseId { get; set; }

    public required CaseStatus FromStatus { get; set; }

    public required CaseStatus ToStatus { get; set; }

    public required string Actor { get; set; }

    public string? Notes { get; set; }

    public required DateTimeOffset ChangedAt { get; set; }
}

public sealed class AnalystNoteRecord
{
    public required Guid Id { get; set; }

    public required Guid CaseId { get; set; }

    public required string Author { get; set; }

    public required string Body { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }
}

public sealed class CustomerProfileRecord
{
    public required string AccountId { get; set; }

    public required string CustomerId { get; set; }

    public required string HomeCountry { get; set; }

    public required decimal AverageTransactionAmount { get; set; }

    public required decimal MedianTransactionAmount { get; set; }

    public required decimal AverageDailySpend { get; set; }

    public required string Currency { get; set; }

    public required string CommonCountriesJson { get; set; }

    public required string CommonMerchantsJson { get; set; }

    public required string CommonCategoriesJson { get; set; }

    public required string CommonDevicesJson { get; set; }

    public required string TypicalHoursJson { get; set; }

    public required decimal TransactionsPerDay { get; set; }

    public required DateTimeOffset AccountOpenedAt { get; set; }

    public required int LifetimeTransactionCount { get; set; }

    public required string RecentAmountsJson { get; set; }

    public required DateTimeOffset UpdatedAt { get; set; }
}

public sealed class KnownDeviceRecord
{
    public required Guid Id { get; set; }

    public required string CustomerId { get; set; }

    public required string DeviceId { get; set; }

    public required DateTimeOffset FirstSeen { get; set; }

    public required DateTimeOffset LastSeen { get; set; }

    public required bool Trusted { get; set; }

    public required int TransactionCount { get; set; }
}

public sealed class AuditEventRecord
{
    public required Guid Id { get; set; }

    public required string Actor { get; set; }

    public required string Action { get; set; }

    public required string EntityType { get; set; }

    public required string EntityId { get; set; }

    public required string DetailsJson { get; set; }

    public required DateTimeOffset OccurredAt { get; set; }

    public string? CorrelationId { get; set; }
}

public sealed class OutboxMessageRecord
{
    public required Guid Id { get; set; }

    public required string Topic { get; set; }

    public required string Key { get; set; }

    public required string Payload { get; set; }

    public required string EventType { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }
}

public sealed class InboxMessageRecord
{
    public required Guid EventId { get; set; }

    public required string Consumer { get; set; }

    public required DateTimeOffset ReceivedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class UserRecord
{
    public required Guid Id { get; set; }

    public required string Email { get; set; }

    public required string DisplayName { get; set; }

    public required string PasswordHash { get; set; }

    public required string Role { get; set; }

    public required bool Active { get; set; }
}
