using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using RetailOps.Application.Common;
using RetailOps.Domain.Enums;
using RetailOps.Infrastructure.Auth;

namespace RetailOps.Api.Infrastructure;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid UserId => Guid.Parse(Required(JwtRegisteredClaimNames.Sub));

    public Guid TenantId => Guid.Parse(Required(RetailOpsClaims.TenantId));

    public UserRole Role => Enum.Parse<UserRole>(Required(ClaimTypes.Role));

    public Guid? StoreId => Guid.TryParse(Principal?.FindFirstValue(RetailOpsClaims.StoreId), out var id) ? id : null;

    private string Required(string claim) =>
        Principal?.FindFirstValue(claim) ?? throw new UnauthorizedException($"Missing claim '{claim}'.");
}
