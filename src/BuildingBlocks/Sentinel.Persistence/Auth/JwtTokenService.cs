using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Sentinel.Persistence.Auth;

public sealed class JwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration) => _configuration = configuration;

    public string Issue(UserRecord user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey(_configuration)));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "sentinel",
            audience: _configuration["Jwt:Audience"] ?? "sentinel",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string SigningKey(IConfiguration configuration) =>
        configuration["Jwt:Key"] ?? "sentinel-local-dev-key-change-me-32chars!!";
}

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(string Token, string Email, string DisplayName, string Role);
