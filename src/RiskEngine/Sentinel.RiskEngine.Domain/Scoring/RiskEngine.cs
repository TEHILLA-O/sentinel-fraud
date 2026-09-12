using System.Diagnostics;
using Sentinel.RiskEngine.Domain.Rules;
using Sentinel.SharedKernel;

namespace Sentinel.RiskEngine.Domain.Scoring;

public sealed class DeterministicRiskEngine : IRiskEngine
{
    private readonly IReadOnlyList<IRiskRule> _rules;
    private readonly IDecisionPolicy _policy;
    private readonly IRiskModel _model;

    public DeterministicRiskEngine(
        IEnumerable<IRiskRule> rules,
        IDecisionPolicy policy,
        IRiskModel model)
    {
        _rules = rules.ToList();
        _policy = policy;
        _model = model;
    }

    public async Task<RiskEvaluation> EvaluateAsync(RiskContext context, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        using var activity = SentinelTelemetry.ActivitySource.StartActivity("sentinel.risk.evaluate");
        activity?.SetTag("transaction.id", context.Transaction.TransactionId);
        activity?.SetTag("ruleset.version", context.RulesetVersion);

        var results = new List<RiskRuleResult>();
        foreach (var rule in _rules)
        {
            var parameters = context.ParametersFor(rule.Code);
            if (!parameters.Enabled)
            {
                continue;
            }

            var result = await rule.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (result.Triggered)
            {
                results.Add(result);
                SentinelTelemetry.RuleTrigger.Add(1, new KeyValuePair<string, object?>("rule", rule.Code));
            }
        }

        var raw = results.Sum(r => r.Score);
        var model = await _model.ScoreAsync(context, cancellationToken).ConfigureAwait(false);
        var combined = Combine(raw, model);
        var capped = ThresholdDecisionPolicy.Cap(combined);
        var level = ThresholdDecisionPolicy.LevelFor(capped);
        var decision = _policy.Decide(capped, level, results);
        var elapsed = Stopwatch.GetElapsedTime(started);

        var evidence = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rule in results)
        {
            foreach (var pair in rule.Evidence)
            {
                evidence[$"{rule.RuleCode}.{pair.Key}"] = pair.Value;
            }
        }

        if (model.Score > 0)
        {
            evidence["model.score"] = model.Score.ToString();
            evidence["model.confidence"] = model.Confidence.ToString("N2");
        }

        SentinelTelemetry.RiskProcessingDuration.Record(elapsed.TotalMilliseconds);
        return new RiskEvaluation
        {
            RawScore = raw,
            RiskScore = capped,
            RiskLevel = level,
            Decision = decision,
            TriggeredRules = results,
            Reasons = results.Select(r => r.Reason).ToArray(),
            Evidence = evidence,
            ProcessingTimeMs = (long)elapsed.TotalMilliseconds,
            ModelVersion = model.ModelVersion,
            RulesetVersion = context.RulesetVersion,
            EvaluatedAt = context.EvaluatedAt
        };
    }

    /// <summary>
    /// Deterministic score is authoritative. Model contribution is additive only when
    /// confidence is at least 0.6, and is capped at 15 points.
    /// </summary>
    public static int Combine(int deterministicScore, ModelRiskResult model)
    {
        if (model.Confidence < 0.6 || model.Score <= 0)
        {
            return deterministicScore;
        }

        var contribution = Math.Min(15, (int)Math.Round(model.Score * 0.15));
        return deterministicScore + contribution;
    }
}

public sealed class NoOpRiskModel : IRiskModel
{
    public const string Version = "noop-1.0.0";

    public Task<ModelRiskResult> ScoreAsync(RiskContext context, CancellationToken cancellationToken) =>
        Task.FromResult(ModelRiskResult.None(Version));
}
