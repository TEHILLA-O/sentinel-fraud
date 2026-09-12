using Sentinel.Contracts;
using Sentinel.RiskEngine.Domain.Models;

namespace Sentinel.RiskEngine.Domain.Rules;

public interface IRiskRule
{
    string Code { get; }

    ValueTask<RiskRuleResult> EvaluateAsync(RiskContext context, CancellationToken cancellationToken);
}

public sealed record RiskRuleResult
{
    public required string RuleCode { get; init; }

    public required bool Triggered { get; init; }

    public required int Score { get; init; }

    public required string Reason { get; init; }

    public IReadOnlyDictionary<string, string> Evidence { get; init; } = new Dictionary<string, string>();

    public static RiskRuleResult NotTriggered(string code) => new()
    {
        RuleCode = code,
        Triggered = false,
        Score = 0,
        Reason = "Rule did not trigger."
    };

    public static RiskRuleResult Hit(
        string code,
        int score,
        string reason,
        IReadOnlyDictionary<string, string>? evidence = null) => new()
    {
        RuleCode = code,
        Triggered = true,
        Score = Math.Max(0, score),
        Reason = reason,
        Evidence = evidence ?? new Dictionary<string, string>()
    };
}

public sealed record RiskContext
{
    public required TransactionSnapshot Transaction { get; init; }

    public required CustomerBehaviourProfile Profile { get; init; }

    public required VelocitySnapshot Velocity { get; init; }

    public required IReadOnlyList<KnownDeviceSnapshot> KnownDevices { get; init; }

    public required IReadOnlyList<HistoricalTransaction> RecentTransactions { get; init; }

    public required IReadOnlyDictionary<string, RuleParameterSet> RuleParameters { get; init; }

    public required string RulesetVersion { get; init; }

    public required DateTimeOffset EvaluatedAt { get; init; }

    public RuleParameterSet ParametersFor(string ruleCode) =>
        RuleParameters.TryGetValue(ruleCode, out var set)
            ? set
            : new RuleParameterSet { RuleCode = ruleCode, Enabled = true, Score = 0 };
}

public static class RuleCodes
{
    public const string HighValue = "HIGH_VALUE";
    public const string HighVelocity = "HIGH_VELOCITY";
    public const string ForeignCountry = "FOREIGN_LOCATION";
    public const string UnknownDevice = "UNKNOWN_DEVICE";
    public const string CardNotPresent = "CARD_NOT_PRESENT";
    public const string ImpossibleTravel = "IMPOSSIBLE_TRAVEL";
    public const string MerchantRisk = "MERCHANT_RISK";
    public const string UnusualTime = "UNUSUAL_TIME";
    public const string NewAccount = "NEW_ACCOUNT";
    public const string BehaviourDeviation = "BEHAVIOURAL_DEVIATION";
    public const string RapidCountryChange = "RAPID_COUNTRY_CHANGE";
    public const string RepeatedDecline = "REPEATED_DECLINE";
    public const string RoundAmount = "ROUND_AMOUNT";

    public static readonly IReadOnlyList<string> All =
    [
        HighValue,
        HighVelocity,
        ForeignCountry,
        UnknownDevice,
        CardNotPresent,
        ImpossibleTravel,
        MerchantRisk,
        UnusualTime,
        NewAccount,
        BehaviourDeviation,
        RapidCountryChange,
        RepeatedDecline,
        RoundAmount
    ];
}

public sealed record RiskEvaluation
{
    public required int RawScore { get; init; }

    public required int RiskScore { get; init; }

    public required RiskLevel RiskLevel { get; init; }

    public required RiskDecision Decision { get; init; }

    public required IReadOnlyList<RiskRuleResult> TriggeredRules { get; init; }

    public required IReadOnlyList<string> Reasons { get; init; }

    public required IReadOnlyDictionary<string, string> Evidence { get; init; }

    public required long ProcessingTimeMs { get; init; }

    public required string ModelVersion { get; init; }

    public required string RulesetVersion { get; init; }

    public required DateTimeOffset EvaluatedAt { get; init; }
}

public interface IRiskEngine
{
    Task<RiskEvaluation> EvaluateAsync(RiskContext context, CancellationToken cancellationToken);
}

public interface IDecisionPolicy
{
    RiskDecision Decide(int score, RiskLevel level, IReadOnlyList<RiskRuleResult> triggeredRules);
}

public interface IRiskModel
{
    Task<ModelRiskResult> ScoreAsync(RiskContext context, CancellationToken cancellationToken);
}

public sealed record ModelRiskResult
{
    public required int Score { get; init; }

    public required double Confidence { get; init; }

    public required string ModelVersion { get; init; }

    public string? Reason { get; init; }

    public static ModelRiskResult None(string version) => new()
    {
        Score = 0,
        Confidence = 0,
        ModelVersion = version
    };
}
