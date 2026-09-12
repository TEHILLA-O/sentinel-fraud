using System.Security.Claims;
using System.Text.Json;
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
}).AllowAnonymous().WithTags("Authentication");

app.MapGet("/api/v1/cases", async (SentinelDbContext db, CaseStatus? status, int page = 1, int pageSize = 25) =>
{
    var request = new PageRequest { Page = page, PageSize = pageSize }.Normalize();
    var query = db.Cases.AsNoTracking().AsQueryable();
    if (status.HasValue)
    {
        query = query.Where(c => c.Status == status);
    }

    var total = await query.CountAsync();
    var items = await query.OrderByDescending(c => c.CreatedAt)
        .Skip(request.Skip)
        .Take(request.PageSize)
        .ToListAsync();
    return Results.Ok(new PagedResult<FraudCaseRecord>(items, total, request.Page, request.PageSize));
}).RequireAuthorization(SentinelPolicies.ViewCases).WithTags("Cases");

app.MapGet("/api/v1/cases/{caseId:guid}", async (Guid caseId, SentinelDbContext db) =>
{
    var fraudCase = await db.Cases.AsNoTracking().FirstOrDefaultAsync(c => c.CaseId == caseId);
    if (fraudCase is null)
    {
        return Results.NotFound();
    }

    var history = await db.CaseHistory.AsNoTracking().Where(h => h.CaseId == caseId).OrderBy(h => h.ChangedAt).ToListAsync();
    var notes = await db.AnalystNotes.AsNoTracking().Where(n => n.CaseId == caseId).OrderBy(n => n.CreatedAt).ToListAsync();
    var decision = await db.RiskDecisions.AsNoTracking().FirstOrDefaultAsync(d => d.TransactionId == fraudCase.TransactionId);
    var transaction = await db.Transactions.AsNoTracking().FirstOrDefaultAsync(t => t.TransactionId == fraudCase.TransactionId);
    return Results.Ok(new { fraudCase, transaction, decision, history, notes });
}).RequireAuthorization(SentinelPolicies.ViewCases).WithTags("Cases");

app.MapPost("/api/v1/cases/{caseId:guid}/notes", async (Guid caseId, NoteRequest request, ClaimsPrincipal user, SentinelDbContext db) =>
{
    if (!await db.Cases.AnyAsync(c => c.CaseId == caseId))
    {
        return Results.NotFound();
    }

    db.AnalystNotes.Add(new AnalystNoteRecord
    {
        Id = Guid.CreateVersion7(),
        CaseId = caseId,
        Author = user.Identity?.Name ?? "analyst",
        Body = request.Body,
        CreatedAt = DateTimeOffset.UtcNow
    });
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization(SentinelPolicies.ReviewCases).WithTags("Cases");

app.MapPost("/api/v1/cases/{caseId:guid}/assign", async (Guid caseId, AssignRequest request, ClaimsPrincipal user, SentinelDbContext db) =>
    await Transition(db, caseId, CaseStatus.Assigned, user, request.Analyst, $"Assigned to {request.Analyst}"))
    .RequireAuthorization(SentinelPolicies.ReviewCases).WithTags("Cases");

app.MapPost("/api/v1/cases/{caseId:guid}/investigate", async (Guid caseId, ClaimsPrincipal user, SentinelDbContext db) =>
    await Transition(db, caseId, CaseStatus.Investigating, user, null, "Investigation started"))
    .RequireAuthorization(SentinelPolicies.ReviewCases).WithTags("Cases");

app.MapPost("/api/v1/cases/{caseId:guid}/escalate", async (Guid caseId, ClaimsPrincipal user, SentinelDbContext db) =>
    await Transition(db, caseId, CaseStatus.Escalated, user, null, "Escalated"))
    .RequireAuthorization(SentinelPolicies.EscalateCases).WithTags("Cases");

app.MapPost("/api/v1/cases/{caseId:guid}/confirm-fraud", async (
    Guid caseId,
    NoteRequest request,
    ClaimsPrincipal user,
    SentinelDbContext db,
    IEventPublisher publisher) =>
{
    var result = await Transition(db, caseId, CaseStatus.ConfirmedFraud, user, null, request.Body);
    await PublishFeedback(db, publisher, caseId, FeedbackType.FraudConfirmed, user, request.Body);
    return result;
}).RequireAuthorization(SentinelPolicies.ReviewCases).WithTags("Cases");

app.MapPost("/api/v1/cases/{caseId:guid}/false-positive", async (
    Guid caseId,
    NoteRequest request,
    ClaimsPrincipal user,
    SentinelDbContext db,
    IEventPublisher publisher) =>
{
    var result = await Transition(db, caseId, CaseStatus.FalsePositive, user, null, request.Body);
    await PublishFeedback(db, publisher, caseId, FeedbackType.FalsePositiveConfirmed, user, request.Body);
    return result;
}).RequireAuthorization(SentinelPolicies.ReviewCases).WithTags("Cases");

app.MapPost("/api/v1/cases/{caseId:guid}/close", async (
    Guid caseId,
    NoteRequest request,
    ClaimsPrincipal user,
    SentinelDbContext db,
    IEventPublisher publisher) =>
{
    var result = await Transition(db, caseId, CaseStatus.Closed, user, null, request.Body);
    await PublishFeedback(db, publisher, caseId, FeedbackType.CaseClosed, user, request.Body);
    return result;
}).RequireAuthorization(SentinelPolicies.ReviewCases).WithTags("Cases");

app.MapGet("/api/v1/transactions/{transactionId}", async (string transactionId, SentinelDbContext db) =>
{
    var transaction = await db.Transactions.AsNoTracking().FirstOrDefaultAsync(t => t.TransactionId == transactionId);
    if (transaction is null)
    {
        return Results.NotFound();
    }

    var decision = await db.RiskDecisions.AsNoTracking().FirstOrDefaultAsync(d => d.TransactionId == transactionId);
    var history = await db.Transactions.AsNoTracking()
        .Where(t => t.AccountId == transaction.AccountId)
        .OrderByDescending(t => t.Timestamp)
        .Take(50)
        .ToListAsync();
    return Results.Ok(new { transaction, decision, history });
}).RequireAuthorization(SentinelPolicies.ViewCases).WithTags("Transactions");

app.MapGet("/api/v1/profiles/{accountId}", async (string accountId, SentinelDbContext db) =>
{
    var profile = await db.CustomerProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.AccountId == accountId);
    if (profile is null)
    {
        return Results.NotFound();
    }

    var devices = await db.KnownDevices.AsNoTracking().Where(d => d.CustomerId == profile.CustomerId).ToListAsync();
    return Results.Ok(new { profile, devices });
}).RequireAuthorization(SentinelPolicies.ViewCases).WithTags("Profiles");

app.Run();

static async Task<IResult> Transition(
    SentinelDbContext db,
    Guid caseId,
    CaseStatus to,
    ClaimsPrincipal user,
    string? assignee,
    string? notes)
{
    var fraudCase = await db.Cases.FirstOrDefaultAsync(c => c.CaseId == caseId);
    if (fraudCase is null)
    {
        return Results.NotFound();
    }

    var from = fraudCase.Status;
    fraudCase.Status = to;
    fraudCase.UpdatedAt = DateTimeOffset.UtcNow;
    if (!string.IsNullOrWhiteSpace(assignee))
    {
        fraudCase.AssignedAnalyst = assignee;
    }

    if (to is CaseStatus.ConfirmedFraud)
    {
        fraudCase.AnalystDecision = RiskDecision.Block;
        SentinelTelemetry.CasesOpen.Add(-1);
    }
    else if (to is CaseStatus.FalsePositive or CaseStatus.Closed)
    {
        fraudCase.AnalystDecision = to == CaseStatus.FalsePositive ? RiskDecision.Approve : fraudCase.AnalystDecision;
        if (from is not CaseStatus.Closed and not CaseStatus.ConfirmedFraud and not CaseStatus.FalsePositive)
        {
            SentinelTelemetry.CasesOpen.Add(-1);
        }
    }

    db.CaseHistory.Add(new CaseHistoryRecord
    {
        Id = Guid.CreateVersion7(),
        CaseId = caseId,
        FromStatus = from,
        ToStatus = to,
        Actor = user.Identity?.Name ?? "analyst",
        Notes = notes,
        ChangedAt = DateTimeOffset.UtcNow
    });
    await db.SaveChangesAsync();
    return Results.NoContent();
}

static async Task PublishFeedback(
    SentinelDbContext db,
    IEventPublisher publisher,
    Guid caseId,
    FeedbackType type,
    ClaimsPrincipal user,
    string? notes)
{
    var fraudCase = await db.Cases.AsNoTracking().FirstAsync(c => c.CaseId == caseId);
    var payload = new CaseFeedbackV1
    {
        EventId = Guid.CreateVersion7(),
        CorrelationId = Guid.CreateVersion7().ToString("N"),
        OccurredAt = DateTimeOffset.UtcNow,
        CaseId = caseId,
        TransactionId = fraudCase.TransactionId,
        FeedbackType = type,
        Analyst = user.Identity?.Name ?? "analyst",
        Notes = notes
    };
    await publisher.PublishAsync(
        KafkaTopics.CaseFeedback,
        fraudCase.AccountId,
        SentinelJson.Serialize(payload),
        payload.CorrelationId,
        CancellationToken.None);
}

public sealed record NoteRequest(string Body);

public sealed record AssignRequest(string Analyst);

public partial class Program;
