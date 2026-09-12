using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sentinel.Contracts;
using Sentinel.Persistence;
using Sentinel.Persistence.Auth;
using Sentinel.RiskEngine.Domain.Scoring;
using Sentinel.SharedKernel;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddSentinelPersistence(builder.Configuration);
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

app.MapGet("/api/v1/rules", async (SentinelDbContext db) =>
{
    var active = await db.RulesetVersions.AsNoTracking().FirstAsync(r => r.IsActive);
    var rules = await db.RiskRuleConfigurations.AsNoTracking()
        .Where(r => r.RulesetVersion == active.Version)
        .OrderBy(r => r.RuleCode)
        .ToListAsync();
    return Results.Ok(new { active.Version, rules });
}).RequireAuthorization(SentinelPolicies.ReadOnlyAudit).WithTags("Risk Rules");

app.MapPost("/api/v1/rulesets", async (CreateRulesetRequest request, ClaimsPrincipal user, SentinelDbContext db) =>
{
    var current = await db.RiskRuleConfigurations
        .Where(r => r.RulesetVersion == db.RulesetVersions.Where(v => v.IsActive).Select(v => v.Version).First())
        .ToListAsync();

    foreach (var existing in await db.RulesetVersions.Where(v => v.IsActive).ToListAsync())
    {
        existing.IsActive = false;
    }

    db.RulesetVersions.Add(new RulesetVersionRecord
    {
        Version = request.Version,
        IsActive = true,
        ConfigurationJson = JsonSerializer.Serialize(request.Rules),
        CreatedAt = DateTimeOffset.UtcNow,
        CreatedBy = user.Identity?.Name ?? "risk-manager"
    });

    foreach (var rule in request.Rules)
    {
        db.RiskRuleConfigurations.Add(new RiskRuleConfigurationRecord
        {
            Id = Guid.CreateVersion7(),
            RulesetVersion = request.Version,
            RuleCode = rule.RuleCode,
            Enabled = rule.Enabled,
            Score = rule.Score,
            ParametersJson = JsonSerializer.Serialize(rule.Parameters)
        });
    }

    db.AuditEvents.Add(new AuditEventRecord
    {
        Id = Guid.CreateVersion7(),
        Actor = user.Identity?.Name ?? "risk-manager",
        Action = "ruleset.activate",
        EntityType = "ruleset",
        EntityId = request.Version,
        DetailsJson = JsonSerializer.Serialize(request),
        OccurredAt = DateTimeOffset.UtcNow
    });

    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/rulesets/{request.Version}", new { request.Version });
}).RequireAuthorization(SentinelPolicies.ModifyRiskConfiguration).WithTags("Risk Rules");

app.MapGet("/api/v1/analytics/summary", async (SentinelDbContext db) =>
{
    var since = DateTimeOffset.UtcNow.AddHours(-24);
    var decisions = db.RiskDecisions.AsNoTracking().Where(d => d.CreatedAt >= since);
    var total = await decisions.CountAsync();
    var approved = await decisions.CountAsync(d => d.Decision == RiskDecision.Approve);
    var review = await decisions.CountAsync(d => d.Decision == RiskDecision.Review);
    var blocked = await decisions.CountAsync(d => d.Decision == RiskDecision.Block);
    var avgScore = total == 0 ? 0 : await decisions.AverageAsync(d => (double)d.RiskScore);
    var cases = await db.Cases.AsNoTracking().CountAsync();
    var confirmed = await db.Cases.AsNoTracking().CountAsync(c => c.Status == CaseStatus.ConfirmedFraud);
    var falsePositives = await db.Cases.AsNoTracking().CountAsync(c => c.Status == CaseStatus.FalsePositive);
    var countries = await db.Transactions.AsNoTracking()
        .Join(db.RiskDecisions.AsNoTracking(), t => t.TransactionId, d => d.TransactionId, (t, d) => new { t.Country, d.RiskScore })
        .Where(x => x.RiskScore >= 60)
        .GroupBy(x => x.Country)
        .Select(g => new { Country = g.Key, Count = g.Count() })
        .OrderByDescending(x => x.Count)
        .Take(8)
        .ToListAsync();

    return Results.Ok(new
    {
        windowHours = 24,
        transactionsProcessed = total,
        approvalRate = Rate(approved, total),
        reviewRate = Rate(review, total),
        blockRate = Rate(blocked, total),
        averageRiskScore = Math.Round(avgScore, 2),
        fraudCasesCreated = cases,
        confirmedFraudRate = Rate(confirmed, cases),
        falsePositiveRate = Rate(falsePositives, cases),
        topRiskyCountries = countries
    });
}).RequireAuthorization(SentinelPolicies.ViewCases).WithTags("Analytics");

app.MapGet("/api/v1/audit", async (SentinelDbContext db, int page = 1, int pageSize = 25) =>
{
    var request = new PageRequest { Page = page, PageSize = pageSize }.Normalize();
    var query = db.AuditEvents.AsNoTracking();
    var total = await query.CountAsync();
    var items = await query.OrderByDescending(a => a.OccurredAt).Skip(request.Skip).Take(request.PageSize).ToListAsync();
    return Results.Ok(new PagedResult<AuditEventRecord>(items, total, request.Page, request.PageSize));
}).RequireAuthorization(SentinelPolicies.ReadOnlyAudit).WithTags("Administration");

app.MapGet("/api/v1/health/system", async (SentinelDbContext db) =>
{
    var decisions = await db.RiskDecisions.AsNoTracking().CountAsync();
    var openCases = await db.Cases.AsNoTracking().CountAsync(c =>
        c.Status == CaseStatus.Open || c.Status == CaseStatus.Assigned || c.Status == CaseStatus.Investigating);
    return Results.Ok(new
    {
        status = "ok",
        decisions,
        openCases,
        ruleset = DefaultRuleCatalog.InitialVersion,
        utc = DateTimeOffset.UtcNow
    });
}).WithTags("Administration");

app.Run();

static decimal Rate(int count, int total) => total == 0 ? 0 : Math.Round((decimal)count / total, 4);

public sealed record RulePatch(string RuleCode, bool Enabled, int Score, Dictionary<string, string> Parameters);

public sealed record CreateRulesetRequest(string Version, List<RulePatch> Rules);

public partial class Program;
