using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sentinel.RiskEngine.Application;

namespace Sentinel.Persistence;

public static class PersistenceExtensions
{
    public static IServiceCollection AddSentinelPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("sentinel")
                               ?? configuration.GetConnectionString("Sentinel")
                               ?? "Host=localhost;Port=5432;Database=sentinel;Username=sentinel;Password=sentinel";

        services.AddDbContext<SentinelDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(2), null);
                npgsql.CommandTimeout(30);
            }));

        services.AddScoped<IInboxStore, EfInboxStore>();
        services.AddScoped<IOutboxStore, EfOutboxStore>();
        services.AddScoped<IRiskDecisionRepository, EfRiskDecisionRepository>();
        services.AddScoped<IRuleConfigurationProvider, EfRuleConfigurationProvider>();
        services.AddScoped<ICustomerProfileStore, EfCustomerProfileStore>();
        return services;
    }
}
