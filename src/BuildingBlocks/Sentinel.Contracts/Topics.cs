namespace Sentinel.Contracts;

public static class KafkaTopics
{
    public const string TransactionsRaw = "sentinel.transactions.raw.v1";
    public const string TransactionsValidated = "sentinel.transactions.validated.v1";
    public const string RiskDecisions = "sentinel.risk.decisions.v1";
    public const string Alerts = "sentinel.alerts.v1";
    public const string DeadLetter = "sentinel.deadletter.v1";
    public const string ProfileUpdated = "sentinel.profile.updated.v1";
    public const string CaseFeedback = "sentinel.case.feedback.v1";

    public static readonly IReadOnlyList<string> All =
    [
        TransactionsRaw,
        TransactionsValidated,
        RiskDecisions,
        Alerts,
        DeadLetter,
        ProfileUpdated,
        CaseFeedback
    ];
}

public static class ConsumerGroups
{
    public const string TransactionProcessor = "sentinel.transaction-processor";
    public const string ProfileProcessor = "sentinel.profile-processor";
    public const string DecisionPublisher = "sentinel.decision-publisher";
    public const string LiveFeed = "sentinel.web-live-feed";
}

public static class RedisChannels
{
    public const string LiveDecisions = "sentinel:live:decisions";
    public const string LiveAlerts = "sentinel:live:alerts";
}

public static class EventTypes
{
    public const string TransactionReceived = "sentinel.transaction.received";
    public const string TransactionValidated = "sentinel.transaction.validated";
    public const string RiskDecision = "sentinel.risk.decision";
    public const string AlertRaised = "sentinel.alert.raised";
    public const string DeadLetter = "sentinel.deadletter";
    public const string ProfileUpdated = "sentinel.profile.updated";
    public const string CaseFeedback = "sentinel.case.feedback";
}
