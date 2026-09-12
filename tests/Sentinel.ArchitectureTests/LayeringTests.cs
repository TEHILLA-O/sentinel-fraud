using FluentAssertions;
using NetArchTest.Rules;
using Sentinel.RiskEngine.Domain.Rules;

namespace Sentinel.ArchitectureTests;

public class LayeringTests
{
    [Fact]
    public void Domain_does_not_reference_infrastructure_or_ef()
    {
        var result = Types.InAssembly(typeof(IRiskRule).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Sentinel.RiskEngine.Infrastructure",
                "Microsoft.EntityFrameworkCore",
                "Confluent.Kafka",
                "StackExchange.Redis")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void All_required_rules_are_present()
    {
        var rules = typeof(IRiskRule).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IRiskRule).IsAssignableFrom(t))
            .Select(t => t.Name)
            .ToHashSet();

        rules.Should().Contain([
            "HighValueRule",
            "HighVelocityRule",
            "ForeignCountryRule",
            "UnknownDeviceRule",
            "CardNotPresentRule",
            "ImpossibleTravelRule",
            "MerchantRiskRule",
            "UnusualTimeRule",
            "NewAccountRule",
            "BehaviourDeviationRule",
            "RapidCountryChangeRule",
            "RepeatedDeclineRule",
            "RoundAmountRule"
        ]);
    }
}
