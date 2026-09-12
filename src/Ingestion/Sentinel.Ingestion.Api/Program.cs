using Microsoft.EntityFrameworkCore;
using Sentinel.Contracts;
using Sentinel.Contracts.Events;
using Sentinel.Contracts.Json;
using Sentinel.Eventing;
using Sentinel.Persistence;
using Sentinel.Persistence.Auth;
using Sentinel.SharedKernel;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddSentinelPersistence(builder.Configuration);
builder.Services.AddSentinelEventing(builder.Configuration);
builder.Services.AddSentinelJwtAuth(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();
await app.Services.InitializeDatabaseAsync();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultEndpoints();
app.MapOpenApi();

app.MapPost("/api/v1/auth/login", async (LoginRequest request, SentinelDbContext db, JwtTokenService tokens) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email && u.Active);
    if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new LoginResponse(tokens.Issue(user), user.Email, user.DisplayName, user.Role));
}).WithTags("Authentication").AllowAnonymous();

app.MapPost("/api/v1/transactions", async (
    TransactionReceivedV1 incoming,
    IEventPublisher publisher) =>
{
    if (string.IsNullOrWhiteSpace(incoming.TransactionId) || incoming.Amount <= 0)
    {
        return Results.Problem(
            title: "Invalid transaction",
            detail: "TransactionId and a positive Amount are required.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    var correlationId = string.IsNullOrWhiteSpace(incoming.CorrelationId)
        ? Guid.CreateVersion7().ToString("N")
        : incoming.CorrelationId;

    var message = incoming with
    {
        EventId = incoming.EventId == Guid.Empty ? Guid.CreateVersion7() : incoming.EventId,
        CorrelationId = correlationId,
        OccurredAt = incoming.OccurredAt == default ? DateTimeOffset.UtcNow : incoming.OccurredAt,
        Timestamp = incoming.Timestamp == default ? DateTimeOffset.UtcNow : incoming.Timestamp
    };

    await publisher.PublishAsync(
        KafkaTopics.TransactionsRaw,
        message.AccountId,
        SentinelJson.Serialize(message),
        correlationId,
        CancellationToken.None);

    return Results.Accepted($"/api/v1/transactions/{message.TransactionId}", new
    {
        message.TransactionId,
        message.EventId,
        message.CorrelationId,
        status = "accepted"
    });
}).WithTags("Transactions");

app.MapGet("/api/v1/transactions/{transactionId}", async (string transactionId, SentinelDbContext db) =>
{
    var row = await db.Transactions.AsNoTracking().FirstOrDefaultAsync(x => x.TransactionId == transactionId);
    return row is null ? Results.NotFound() : Results.Ok(row);
}).WithTags("Transactions").RequireAuthorization(SentinelPolicies.ViewCases);

app.Run();

public partial class Program;