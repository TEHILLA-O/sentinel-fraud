var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .AddDatabase("sentinel");

var redis = builder.AddRedis("redis");
var kafka = builder.AddKafka("kafka")
    .WithKafkaUI();

var ingestion = builder.AddProject<Projects.Sentinel_Ingestion_Api>("ingestion")
    .WithReference(postgres)
    .WithReference(redis)
    .WithReference(kafka)
    .WaitFor(postgres)
    .WaitFor(kafka)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.Sentinel_Cases_Api>("cases")
    .WithReference(postgres)
    .WithReference(kafka)
    .WaitFor(postgres)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.Sentinel_Admin_Api>("admin")
    .WithReference(postgres)
    .WaitFor(postgres)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.Sentinel_TransactionProcessor>("transaction-processor")
    .WithReference(postgres)
    .WithReference(redis)
    .WithReference(kafka)
    .WaitFor(postgres)
    .WaitFor(redis)
    .WaitFor(kafka);

builder.AddProject<Projects.Sentinel_ProfileProcessor>("profile-processor")
    .WithReference(postgres)
    .WithReference(redis)
    .WithReference(kafka)
    .WaitFor(postgres)
    .WaitFor(kafka);

builder.AddProject<Projects.Sentinel_DecisionPublisher>("decision-publisher")
    .WithReference(postgres)
    .WithReference(redis)
    .WithReference(kafka)
    .WaitFor(postgres)
    .WaitFor(redis)
    .WaitFor(kafka);

builder.AddProject<Projects.Sentinel_Web>("web")
    .WithReference(postgres)
    .WithReference(redis)
    .WithReference(kafka)
    .WaitFor(postgres)
    .WaitFor(redis)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

builder.Build().Run();
