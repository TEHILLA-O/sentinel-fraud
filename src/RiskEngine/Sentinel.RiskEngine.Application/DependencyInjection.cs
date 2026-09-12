using Microsoft.Extensions.DependencyInjection;
using Sentinel.RiskEngine.Domain.Rules;
using Sentinel.RiskEngine.Domain.Scoring;
using Sentinel.SharedKernel;

namespace Sentinel.RiskEngine.Application;

public static class RiskEngineApplicationExtensions
{
    public static IServiceCollection AddRiskEngineApplication(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ITransactionValidator, TransactionValidator>();
        services.AddSingleton<IDecisionPolicy, ThresholdDecisionPolicy>();
        services.AddSingleton<IRiskModel, NoOpRiskModel>();
        services.AddSingleton<IRiskRule, HighValueRule>();
        services.AddSingleton<IRiskRule, HighVelocityRule>();
        services.AddSingleton<IRiskRule, ForeignCountryRule>();
        services.AddSingleton<IRiskRule, UnknownDeviceRule>();
        services.AddSingleton<IRiskRule, CardNotPresentRule>();
        services.AddSingleton<IRiskRule, ImpossibleTravelRule>();
        services.AddSingleton<IRiskRule, MerchantRiskRule>();
        services.AddSingleton<IRiskRule, UnusualTimeRule>();
        services.AddSingleton<IRiskRule, NewAccountRule>();
        services.AddSingleton<IRiskRule, BehaviourDeviationRule>();
        services.AddSingleton<IRiskRule, RapidCountryChangeRule>();
        services.AddSingleton<IRiskRule, RepeatedDeclineRule>();
        services.AddSingleton<IRiskRule, RoundAmountRule>();
        services.AddSingleton<IRiskEngine, DeterministicRiskEngine>();
        services.AddScoped<TransactionProcessingService>();
        return services;
    }
}
