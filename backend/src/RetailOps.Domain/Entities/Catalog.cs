using RetailOps.Domain.Common;
using RetailOps.Domain.Enums;

namespace RetailOps.Domain.Entities;

public class Tenant : Entity
{
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string Currency { get; set; } = "BOB";
}

public class User : TenantEntity
{
    public required string Email { get; set; }
    public required string FullName { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }

    /// <summary>Managers and cashiers operate a single store; admins see all stores.</summary>
    public Guid? StoreId { get; set; }
    public Store? Store { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Store : TenantEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string City { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Product : TenantEntity
{
    public required string Sku { get; set; }
    public required string Barcode { get; set; }
    public required string Name { get; set; }
    public required string Category { get; set; }
    public decimal Price { get; set; }
    public decimal Cost { get; set; }
    public bool IsActive { get; set; } = true;
}
