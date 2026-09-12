using FluentAssertions;
using Sentinel.Contracts;
using Sentinel.RiskEngine.Domain.Rules;
using Sentinel.RiskEngine.Domain.Scoring;
using Sentinel.SharedKernel;

namespace Sentinel.Domain.Tests;

public class MoneyTests
{
    [Fact]
    public void Rejects_floating_style_precision_by_rounding_to_two_decimals()
    {
        var money = Money.Gbp(10.129m);
        money.Amount.Should().Be(10.13m);
        money.ToMinorUnits().Should().Be(1013);
    }

    [Fact]
    public void Cannot_be_negative()
    {
        var act = () => Money.Gbp(-1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Cannot_mix_currencies()
    {
        var act = () => Money.Gbp(10) + Money.Usd(10);
        act.Should().Throw<InvalidOperationException>();
    }
}

public class DecisionPolicyTests
{
    private readonly ThresholdDecisionPolicy _policy = new();

    [Theory]
    [InlineData(0, RiskLevel.Low, RiskDecision.Approve)]
    [InlineData(29, RiskLevel.Low, RiskDecision.Approve)]
    [InlineData(30, RiskLevel.Medium, RiskDecision.Approve)]
    [InlineData(59, RiskLevel.Medium, RiskDecision.Approve)]
    [InlineData(60, RiskLevel.High, RiskDecision.Review)]
    [InlineData(79, RiskLevel.High, RiskDecision.Review)]
    [InlineData(80, RiskLevel.Critical, RiskDecision.Block)]
    [InlineData(100, RiskLevel.Critical, RiskDecision.Block)]
    public void Maps_score_bands(int score, RiskLevel level, RiskDecision decision)
    {
        ThresholdDecisionPolicy.LevelFor(score).Should().Be(level);
        _policy.Decide(score, level, []).Should().Be(decision);
    }

    [Fact]
    public void Medium_score_with_velocity_reviews()
    {
        var triggered = new[]
        {
            RiskRuleResult.Hit(RuleCodes.HighVelocity, 30, "fast")
        };
        _policy.Decide(40, RiskLevel.Medium, triggered).Should().Be(RiskDecision.Review);
    }

    [Fact]
    public void Caps_score_at_100()
    {
        ThresholdDecisionPolicy.Cap(110).Should().Be(100);
        ThresholdDecisionPolicy.Cap(-4).Should().Be(0);
    }
}

public class RiskEngineAggregationTests
{
    [Fact]
    public async Task Aggregates_caps_and_skips_disabled_rules()
    {
        var engine = new DeterministicRiskEngine(
            [new HighValueRule(), new UnknownDeviceRule()],
            new ThresholdDecisionPolicy(),
            new NoOpRiskModel());

        var context = RiskContextFactory.Example(
            amount: 4850m,
            country: "US",
            deviceId: "unknown",
            disable: [RuleCodes.UnknownDevice]);

        var evaluation = await engine.EvaluateAsync(context, CancellationToken.None);
        evaluation.RawScore.Should().Be(20);
        evaluation.RiskScore.Should().Be(20);
        evaluation.Decision.Should().Be(RiskDecision.Approve);
        evaluation.TriggeredRules.Should().ContainSingle(r => r.RuleCode == RuleCodes.HighValue);
        evaluation.RulesetVersion.Should().Be(context.RulesetVersion);
        evaluation.ModelVersion.Should().Be(NoOpRiskModel.Version);
    }

    [Fact]
    public async Task Example_transaction_blocks_with_explanation()
    {
        var engine = new DeterministicRiskEngine(
            [
                new HighValueRule(),
                new ForeignCountryRule(),
                new CardNotPresentRule(),
                new UnknownDeviceRule(),
                new HighVelocityRule(),
                new BehaviourDeviationRule()
            ],
            new ThresholdDecisionPolicy(),
            new NoOpRiskModel());

        var context = RiskContextFactory.PromptExample();
        var evaluation = await engine.EvaluateAsync(context, CancellationToken.None);

        evaluation.RawScore.Should().Be(110);
        evaluation.RiskScore.Should().Be(100);
        evaluation.Decision.Should().Be(RiskDecision.Block);
        evaluation.TriggeredRules.Select(r => r.RuleCode).Should().BeEquivalentTo(
        [
            RuleCodes.HighValue,
            RuleCodes.ForeignCountry,
            RuleCodes.CardNotPresent,
            RuleCodes.UnknownDevice,
            RuleCodes.HighVelocity,
            RuleCodes.BehaviourDeviation
        ]);
    }

    [Fact]
    public void Model_contribution_requires_confidence()
    {
        DeterministicRiskEngine.Combine(80, new ModelRiskResult
        {
            Score = 90,
            Confidence = 0.4,
            ModelVersion = "x"
        }).Should().Be(80);

        DeterministicRiskEngine.Combine(80, new ModelRiskResult
        {
            Score = 90,
            Confidence = 0.8,
            ModelVersion = "x"
        }).Should().Be(94);
    }
}
