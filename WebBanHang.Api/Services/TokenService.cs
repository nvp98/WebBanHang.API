using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace WebBanHang.Api.Services;

public class JwtOptions
{
    public string Issuer { get; set; } = "WebBanHang.Api";
    public string Audience { get; set; } = "WebBanHang.Client";
    /// <summary>HMAC-SHA256 signing key, at least 32 characters. Set via configuration/secret store, never commit a real one.</summary>
    public string Key { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 7;

    public string RefreshAudience => Audience + ":refresh";
    public SymmetricSecurityKey SigningKey => new(Encoding.UTF8.GetBytes(Key));
}

public record AuthTokens(string AccessToken, string RefreshToken, int ExpiresIn);

/// <summary>
/// Stateless JWT access + refresh tokens. The refresh token uses a separate audience so it can never be
/// used as an access token; on refresh the account is re-checked against the database.
/// </summary>
public class TokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _opt = options.Value;

    public AuthTokens Issue(int userId, string role, string name, string? email)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role),
            new(ClaimTypes.Name, name),
        };
        if (!string.IsNullOrEmpty(email)) claims.Add(new Claim(ClaimTypes.Email, email));

        var access = Write(claims, _opt.Audience, TimeSpan.FromMinutes(_opt.AccessTokenMinutes));
        var refresh = Write(claims, _opt.RefreshAudience, TimeSpan.FromDays(_opt.RefreshTokenDays));
        return new AuthTokens(access, refresh, _opt.AccessTokenMinutes * 60);
    }

    /// <summary>Returns the principal of a valid refresh token, or null.</summary>
    public ClaimsPrincipal? ReadRefreshToken(string token)
    {
        var parameters = ValidationParameters(_opt);
        parameters.ValidAudience = _opt.RefreshAudience;
        try
        {
            // Default inbound mapping turns the short JWT names (nameid, role…) back into ClaimTypes.*,
            // matching JwtBearerOptions.MapInboundClaims = true in Program.cs.
            return new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            return null;
        }
    }

    public static TokenValidationParameters ValidationParameters(JwtOptions opt) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = opt.Issuer,
        ValidateAudience = true,
        ValidAudience = opt.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = opt.SigningKey,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role,
    };

    private string Write(IEnumerable<Claim> claims, string audience, TimeSpan lifetime)
    {
        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: new SigningCredentials(_opt.SigningKey, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
