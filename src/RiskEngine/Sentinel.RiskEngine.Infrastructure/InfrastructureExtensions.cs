using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sentinel.RiskEngine.Application;
using Sentinel.RiskEngine.Domain.Rules;
using StackExchange.Redis;

namespace Sentinel.RiskEngine.Infrastructure;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddRiskEngineInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var redis = configuration.GetConnectionString("redis")
                    ?? configuration["Redis:ConnectionString"]
                    ?? "localhost:6379";

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redis));
        services.AddSingleton<IVelocityStore, RedisVelocityStore>();

        if (string.Equals(configuration["Risk:UseMlNet"], "true", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IRiskModel, MlNetAnomalyRiskModel>();
        }

        return services;
    }
}
