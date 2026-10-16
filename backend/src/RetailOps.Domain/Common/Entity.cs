namespace RetailOps.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();
}

/// <summary>Marks an entity that belongs to a single tenant. The persistence layer filters and stamps these automatically.</summary>
public interface ITenantScoped
{
    Guid TenantId { get; set; }
}

public abstract class TenantEntity : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
}
