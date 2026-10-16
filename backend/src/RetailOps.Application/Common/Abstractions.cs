using Microsoft.EntityFrameworkCore;
using RetailOps.Domain.Entities;
using RetailOps.Domain.Enums;

namespace RetailOps.Application.Common;

public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<User> Users { get; }
    DbSet<Store> Stores { get; }
    DbSet<Product> Products { get; }
    DbSet<StockLevel> StockLevels { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<Sale> Sales { get; }
    DbSet<Transfer> Transfers { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>The authenticated caller, resolved from the JWT. Drives tenant filtering and store-level authorization.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    Guid TenantId { get; }
    UserRole Role { get; }
    Guid? StoreId { get; }
}

public interface IJwtTokenGenerator
{
    (string Token, DateTimeOffset ExpiresAt) Generate(User user);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public static class QueryableExtensions
{
    public static async Task<PagedResult<T>> ToPagedAsync<T>(this IQueryable<T> query, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<T>(items, page, pageSize, total);
    }
}
