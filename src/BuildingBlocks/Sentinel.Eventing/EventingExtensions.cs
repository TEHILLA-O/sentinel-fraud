using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Sentinel.Eventing;

public static class EventingExtensions
{
    public static IServiceCollection AddSentinelEventing(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<KafkaOptions>()
            .Bind(configuration.GetSection(KafkaOptions.SectionName))
            .PostConfigure(options =>
            {
                var fromConnection = configuration.GetConnectionString("kafka")
                                     ?? configuration.GetConnectionString("Kafka");
                if (!string.IsNullOrWhiteSpace(fromConnection))
                {
                    options.BootstrapServers = fromConnection.Replace("localhost", "127.0.0.1", StringComparison.OrdinalIgnoreCase);
                }
            });
        services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
        services.AddHostedService<KafkaTopicBootstrapper>();
        return services;
    }
}
