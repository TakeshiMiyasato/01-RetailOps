using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RetailOps.Application.Common;
using RetailOps.Domain.Entities;

namespace RetailOps.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "retailops";
    public string Audience { get; set; } = "retailops-clients";
    public string SigningKey { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 480;
}

public static class RetailOpsClaims
{
    public const string TenantId = "tenant_id";
    public const string StoreId = "store_id";
}

public sealed class JwtTokenGenerator(IOptions<JwtOptions> options, TimeProvider clock) : IJwtTokenGenerator
{
    public (string Token, DateTimeOffset ExpiresAt) Generate(User user)
    {
        var o = options.Value;
        var expiresAt = clock.GetUtcNow().AddMinutes(o.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(RetailOpsClaims.TenantId, user.TenantId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (user.StoreId is not null)
            claims.Add(new Claim(RetailOpsClaims.StoreId, user.StoreId.Value.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.SigningKey));
        var token = new JwtSecurityToken(
            issuer: o.Issuer,
            audience: o.Audience,
            claims: claims,
            notBefore: clock.GetUtcNow().UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
