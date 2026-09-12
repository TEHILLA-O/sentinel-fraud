using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Sentinel.Persistence.Auth;
using Sentinel.SharedKernel;

namespace Microsoft.Extensions.Hosting;

public static class SentinelAuthExtensions
{
    public static IServiceCollection AddSentinelJwtAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<JwtTokenService>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = configuration["Jwt:Issuer"] ?? "sentinel",
                ValidAudience = configuration["Jwt:Audience"] ?? "sentinel",
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(JwtTokenService.SigningKey(configuration)))
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(SentinelPolicies.ViewCases, p =>
                p.RequireRole(
                    SentinelRoles.FraudAnalyst,
                    SentinelRoles.SeniorAnalyst,
                    SentinelRoles.RiskManager,
                    SentinelRoles.Administrator,
                    SentinelRoles.Auditor));
            options.AddPolicy(SentinelPolicies.ReviewCases, p =>
                p.RequireRole(SentinelRoles.FraudAnalyst, SentinelRoles.SeniorAnalyst, SentinelRoles.Administrator));
            options.AddPolicy(SentinelPolicies.EscalateCases, p =>
                p.RequireRole(SentinelRoles.SeniorAnalyst, SentinelRoles.Administrator));
            options.AddPolicy(SentinelPolicies.ModifyRiskConfiguration, p =>
                p.RequireRole(SentinelRoles.RiskManager, SentinelRoles.Administrator));
            options.AddPolicy(SentinelPolicies.AdministerSystem, p =>
                p.RequireRole(SentinelRoles.Administrator));
            options.AddPolicy(SentinelPolicies.ReadOnlyAudit, p =>
                p.RequireRole(SentinelRoles.Auditor, SentinelRoles.Administrator, SentinelRoles.RiskManager));
        });

        return services;
    }
}
