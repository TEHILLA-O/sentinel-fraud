using Sentinel.Eventing;
using Sentinel.Persistence;
using Sentinel.ProfileProcessor;
using Sentinel.RiskEngine.Application;
using Sentinel.RiskEngine.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddSentinelPersistence(builder.Configuration);
builder.Services.AddRiskEngineApplication();
builder.Services.AddRiskEngineInfrastructure(builder.Configuration);
builder.Services.AddSentinelEventing(builder.Configuration);
builder.Services.AddHostedService<ProfileUpdateConsumer>();
builder.Services.AddHostedService<OutboxDispatcher>();

var host = builder.Build();
await host.Services.InitializeDatabaseAsync();
await host.RunAsync();
