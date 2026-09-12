using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.SignalR;
using Sentinel.Contracts;
using Sentinel.Eventing;
using Sentinel.Persistence;
using Sentinel.RiskEngine.Infrastructure;
using Sentinel.Web.Components;
using Sentinel.Web.Components.Pages;
using Sentinel.Web.Realtime;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddSentinelPersistence(builder.Configuration);
builder.Services.AddRiskEngineInfrastructure(builder.Configuration);
builder.Services.AddSentinelEventing(builder.Configuration);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddSignalR();
builder.Services.AddHostedService<LiveDecisionListener>();

var app = builder.Build();
await app.Services.InitializeDatabaseAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapDefaultEndpoints();
app.MapHub<LiveDecisionHub>("/hubs/live");
app.MapLogout();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.Run();
