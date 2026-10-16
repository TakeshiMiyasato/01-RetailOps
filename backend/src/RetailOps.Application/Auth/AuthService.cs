using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RetailOps.Application.Common;
using RetailOps.Domain.Entities;

namespace RetailOps.Application.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record UserProfile(Guid Id, string Email, string FullName, string Role, Guid TenantId, string TenantName, Guid? StoreId, string? StoreName);

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, UserProfile User);

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class AuthService(
    IAppDbContext db,
    IPasswordHasher<User> hasher,
    IJwtTokenGenerator tokens,
    ICurrentUser currentUser,
    IValidator<LoginRequest> validator)
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        // Login happens before we know the tenant, so it is the one query that bypasses the tenant filter.
        var user = await db.Users.IgnoreQueryFilters()
            .Include(u => u.Store)
            .FirstOrDefaultAsync(u => u.Email == request.Email.Trim().ToLowerInvariant(), ct);

        if (user is null || !user.IsActive ||
            hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            throw new UnauthorizedException("Invalid email or password.");

        var tenant = await db.Tenants.FirstAsync(t => t.Id == user.TenantId, ct);
        var (token, expiresAt) = tokens.Generate(user);
        return new LoginResponse(token, expiresAt, ToProfile(user, tenant));
    }

    public async Task<UserProfile> MeAsync(CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.Store).FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct)
                   ?? throw new NotFoundException("User", currentUser.UserId);
        var tenant = await db.Tenants.FirstAsync(t => t.Id == user.TenantId, ct);
        return ToProfile(user, tenant);
    }

    private static UserProfile ToProfile(User user, Tenant tenant) =>
        new(user.Id, user.Email, user.FullName, user.Role.ToString(), tenant.Id, tenant.Name, user.StoreId, user.Store?.Name);
}
