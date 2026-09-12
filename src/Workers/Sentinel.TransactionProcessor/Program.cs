using Sentinel.Eventing;
using Sentinel.Persistence;
using Sentinel.RiskEngine.Application;
using Sentinel.RiskEngine.Infrastructure;
using Sentinel.TransactionProcessor;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddSentinelPersistence(builder.Configuration);
builder.Services.AddRiskEngineApplication();
builder.Services.AddRiskEngineInfrastructure(builder.Configuration);
builder.Services.AddSentinelEventing(builder.Configuration);
builder.Services.AddHostedService<RawTransactionConsumer>();
builder.Services.AddHostedService<OutboxDispatcher>();

var host = builder.Build();
await host.Services.InitializeDatabaseAsync();
await host.RunAsync();
