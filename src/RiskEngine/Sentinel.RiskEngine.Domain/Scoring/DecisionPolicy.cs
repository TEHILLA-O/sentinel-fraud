using Sentinel.Contracts;
using Sentinel.RiskEngine.Domain.Rules;

namespace Sentinel.RiskEngine.Domain.Scoring;

public sealed class ThresholdDecisionPolicy : IDecisionPolicy
{
    public const int MaxScore = 100;

    public static RiskLevel LevelFor(int score) => score switch
    {
        <= 29 => RiskLevel.Low,
        <= 59 => RiskLevel.Medium,
        <= 79 => RiskLevel.High,
        _ => RiskLevel.Critical
    };

    public RiskDecision Decide(int score, RiskLevel level, IReadOnlyList<RiskRuleResult> triggeredRules)
    {
        return level switch
        {
            RiskLevel.Low => RiskDecision.Approve,
            RiskLevel.Medium => DecideMedium(triggeredRules),
            RiskLevel.High => RiskDecision.Review,
            RiskLevel.Critical => RiskDecision.Block,
            _ => RiskDecision.Review
        };
    }

    private static RiskDecision DecideMedium(IReadOnlyList<RiskRuleResult> triggeredRules)
    {
        var codes = triggeredRules.Select(r => r.RuleCode).ToHashSet(StringComparer.Ordinal);
        if (codes.Contains(RuleCodes.ImpossibleTravel) ||
            codes.Contains(RuleCodes.RepeatedDecline) ||
            codes.Contains(RuleCodes.HighVelocity))
        {
            return RiskDecision.Review;
        }

        return RiskDecision.Approve;
    }

    public static int Cap(int rawScore) => Math.Clamp(rawScore, 0, MaxScore);
}

public sealed class DefaultRuleCatalog
{
    public const string InitialVersion = "ruleset-1.0.0";

    public static IReadOnlyList<RuleDefinition> Defaults { get; } =
    [
        new(RuleCodes.HighValue, true, 20, new Dictionary<string, string> { ["threshold"] = "3000" }),
        new(RuleCodes.HighVelocity, true, 30, new Dictionary<string, string>
        {
            ["fiveMinuteCount"] = "5",
            ["oneMinuteCount"] = "3"
        }),
        new(RuleCodes.ForeignCountry, true, 20, new Dictionary<string, string>()),
        new(RuleCodes.UnknownDevice, true, 15, new Dictionary<string, string>()),
        new(RuleCodes.CardNotPresent, true, 10, new Dictionary<string, string>()),
        new(RuleCodes.ImpossibleTravel, true, 40, new Dictionary<string, string>
        {
            ["maxSpeedKmh"] = "900",
            ["bufferMinutes"] = "30"
        }),
        new(RuleCodes.MerchantRisk, true, 15, new Dictionary<string, string>()),
        new(RuleCodes.UnusualTime, true, 10, new Dictionary<string, string>()),
        new(RuleCodes.NewAccount, true, 15, new Dictionary<string, string> { ["days"] = "14" }),
        new(RuleCodes.BehaviourDeviation, true, 15, new Dictionary<string, string> { ["multiplier"] = "8" }),
        new(RuleCodes.RapidCountryChange, true, 20, new Dictionary<string, string> { ["distinctCountries"] = "2" }),
        new(RuleCodes.RepeatedDecline, true, 20, new Dictionary<string, string> { ["failures"] = "3" }),
        new(RuleCodes.RoundAmount, true, 8, new Dictionary<string, string> { ["minimumAmount"] = "1000" })
    ];
}

public sealed record RuleDefinition(
    string Code,
    bool Enabled,
    int Score,
    IReadOnlyDictionary<string, string> Parameters);
