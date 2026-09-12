using System.Diagnostics;
using FluentAssertions;
using Sentinel.Domain.Tests;
using Sentinel.RiskEngine.Domain.Rules;
using Sentinel.RiskEngine.Domain.Scoring;
using Sentinel.SharedKernel;

namespace Sentinel.PerformanceTests;

public class EngineThroughputTests
{
    [Fact]
    public async Task Measures_in_process_latency_percentiles()
    {
        var engine = new DeterministicRiskEngine(
            [
                new HighValueRule(),
                new HighVelocityRule(),
                new ForeignCountryRule(),
                new UnknownDeviceRule(),
                new CardNotPresentRule(),
                new ImpossibleTravelRule(),
                new MerchantRiskRule(),
                new UnusualTimeRule(),
                new NewAccountRule(),
                new BehaviourDeviationRule(),
                new RapidCountryChangeRule(),
                new RepeatedDeclineRule(),
                new RoundAmountRule()
            ],
            new ThresholdDecisionPolicy(),
            new NoOpRiskModel());

        var tracker = new LatencyTracker();
        for (var i = 0; i < 250; i++)
        {
            var started = Stopwatch.GetTimestamp();
            await engine.EvaluateAsync(RiskContextFactory.PromptExample(), CancellationToken.None);
            tracker.Record((long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }

        var snapshot = tracker.Snapshot();
        snapshot.P99.Should().BeLessThan(250);
        Console.WriteLine($"In-process engine P50={snapshot.P50}ms P95={snapshot.P95}ms P99={snapshot.P99}ms");
    }
}
