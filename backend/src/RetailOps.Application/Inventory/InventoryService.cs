using FluentValidation;
using Microsoft.EntityFrameworkCore;
using RetailOps.Application.Common;
using RetailOps.Domain.Entities;
using RetailOps.Domain.Enums;

namespace RetailOps.Application.Inventory;

public sealed record StockItemDto(
    Guid ProductId, string Sku, string Barcode, string Name, string Category,
    decimal Price, int Quantity, int ReorderPoint, bool IsLow);

public sealed record StockAdjustmentRequest(Guid ProductId, int Quantity, string Reason, bool IsReceipt = false);

public sealed record StockMovementDto(Guid Id, string Sku, string ProductName, int Quantity, string Type, string Reference, string? Note, DateTimeOffset CreatedAt);

/// <summary>
/// A physical count captured on the mobile scanner, possibly offline. <see cref="ClientCountId"/> makes the
/// submission idempotent so a device can safely retry the sync after a dropped connection.
/// </summary>
public sealed record StockCountRequest(Guid ClientCountId, IReadOnlyList<StockCountLine> Lines);

public sealed record StockCountLine(Guid ProductId, int CountedQuantity);

public sealed record StockCountResult(Guid ClientCountId, int LinesProcessed, int AdjustmentsMade, bool AlreadyProcessed);

public sealed class StockAdjustmentRequestValidator : AbstractValidator<StockAdjustmentRequest>
{
    public StockAdjustmentRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).NotEqual(0);
        RuleFor(x => x.Quantity).GreaterThan(0).When(x => x.IsReceipt).WithMessage("Receipts must add stock.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(200);
    }
}

public sealed class StockCountRequestValidator : AbstractValidator<StockCountRequest>
{
    public StockCountRequestValidator()
    {
        RuleFor(x => x.ClientCountId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty();
            line.RuleFor(l => l.CountedQuantity).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class InventoryService(
    IAppDbContext db,
    ICurrentUser currentUser,
    TimeProvider clock,
    IValidator<StockAdjustmentRequest> adjustmentValidator,
    IValidator<StockCountRequest> countValidator)
{
    public Task<PagedResult<StockItemDto>> GetStockAsync(Guid storeId, string? search, bool lowOnly, int page, int pageSize, CancellationToken ct)
    {
        currentUser.EnsureCanOperate(storeId);

        var query = db.StockLevels.AsNoTracking().Where(s => s.StoreId == storeId && s.Product!.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.Product!.Name.Contains(search) || s.Product.Sku.Contains(search) || s.Product.Barcode == search);
        if (lowOnly)
            query = query.Where(s => s.Quantity <= s.ReorderPoint);

        return query
            .OrderBy(s => s.Product!.Name)
            .Select(s => new StockItemDto(s.ProductId, s.Product!.Sku, s.Product.Barcode, s.Product.Name, s.Product.Category,
                s.Product.Price, s.Quantity, s.ReorderPoint, s.Quantity <= s.ReorderPoint))
            .ToPagedAsync(page, pageSize, ct);
    }

    public async Task<StockItemDto> GetByBarcodeAsync(Guid storeId, string barcode, CancellationToken ct)
    {
        currentUser.EnsureCanOperate(storeId);
        return await db.StockLevels.AsNoTracking()
                   .Where(s => s.StoreId == storeId && s.Product!.Barcode == barcode)
                   .Select(s => new StockItemDto(s.ProductId, s.Product!.Sku, s.Product.Barcode, s.Product.Name, s.Product.Category,
                       s.Product.Price, s.Quantity, s.ReorderPoint, s.Quantity <= s.ReorderPoint))
                   .FirstOrDefaultAsync(ct)
               ?? throw new NotFoundException("Product with barcode", barcode);
    }

    public async Task<StockItemDto> AdjustAsync(Guid storeId, StockAdjustmentRequest request, CancellationToken ct)
    {
        await adjustmentValidator.ValidateAndThrowAsync(request, ct);
        currentUser.EnsureCanOperate(storeId);

        var level = await db.StockLevels.Include(s => s.Product)
                        .FirstOrDefaultAsync(s => s.StoreId == storeId && s.ProductId == request.ProductId, ct)
                    ?? throw new NotFoundException("Stock level", request.ProductId);

        ApplyDelta(level, request.Quantity);
        db.StockMovements.Add(new StockMovement
        {
            StoreId = storeId,
            ProductId = level.ProductId,
            Quantity = request.Quantity,
            Type = request.IsReceipt ? MovementType.Receipt : MovementType.Adjustment,
            Reference = request.IsReceipt ? "RECEIPT" : "ADJUST",
            Note = request.Reason,
            UserId = currentUser.UserId,
            CreatedAt = clock.GetUtcNow()
        });

        await SaveWithConcurrencyAsync(ct);
        var p = level.Product!;
        return new StockItemDto(p.Id, p.Sku, p.Barcode, p.Name, p.Category, p.Price, level.Quantity, level.ReorderPoint, level.IsLow);
    }

    public async Task<StockCountResult> SubmitCountAsync(Guid storeId, StockCountRequest request, CancellationToken ct)
    {
        await countValidator.ValidateAndThrowAsync(request, ct);
        currentUser.EnsureCanOperate(storeId);

        var reference = $"COUNT-{request.ClientCountId:N}";
        if (await db.StockMovements.AnyAsync(m => m.Reference == reference, ct) ||
            await db.StockMovements.AnyAsync(m => m.Reference == reference + "-NOOP", ct))
            return new StockCountResult(request.ClientCountId, request.Lines.Count, 0, AlreadyProcessed: true);

        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var levels = await db.StockLevels.Include(s => s.Product)
            .Where(s => s.StoreId == storeId && productIds.Contains(s.ProductId))
            .ToDictionaryAsync(s => s.ProductId, ct);

        var adjustments = 0;
        foreach (var line in request.Lines.GroupBy(l => l.ProductId).Select(g => g.Last()))
        {
            if (!levels.TryGetValue(line.ProductId, out var level))
                throw new NotFoundException("Stock level", line.ProductId);

            var delta = line.CountedQuantity - level.Quantity;
            if (delta == 0) continue;

            ApplyDelta(level, delta);
            db.StockMovements.Add(new StockMovement
            {
                StoreId = storeId,
                ProductId = level.ProductId,
                Quantity = delta,
                Type = MovementType.Adjustment,
                Reference = reference,
                Note = $"Physical count: system {level.Quantity - delta}, counted {line.CountedQuantity}",
                UserId = currentUser.UserId,
                CreatedAt = clock.GetUtcNow()
            });
            adjustments++;
        }

        // A count that matched the system exactly still has to be remembered, otherwise a retry would look new.
        if (adjustments == 0)
        {
            var first = levels.Values.First();
            db.StockMovements.Add(new StockMovement
            {
                StoreId = storeId, ProductId = first.ProductId, Quantity = 0, Type = MovementType.Adjustment,
                Reference = reference + "-NOOP", Note = "Physical count matched system stock", UserId = currentUser.UserId,
                CreatedAt = clock.GetUtcNow()
            });
        }

        await SaveWithConcurrencyAsync(ct);
        return new StockCountResult(request.ClientCountId, request.Lines.Count, adjustments, AlreadyProcessed: false);
    }

    public Task<PagedResult<StockMovementDto>> GetMovementsAsync(Guid storeId, Guid? productId, int page, int pageSize, CancellationToken ct)
    {
        currentUser.EnsureCanOperate(storeId);
        var query = db.StockMovements.AsNoTracking().Where(m => m.StoreId == storeId && m.Quantity != 0);
        if (productId is not null)
            query = query.Where(m => m.ProductId == productId);

        return query.OrderByDescending(m => m.CreatedAt)
            .Select(m => new StockMovementDto(m.Id, m.Product!.Sku, m.Product.Name, m.Quantity, m.Type.ToString(), m.Reference, m.Note, m.CreatedAt))
            .ToPagedAsync(page, pageSize, ct);
    }

    private static void ApplyDelta(StockLevel level, int delta)
    {
        if (delta > 0) level.Increase(delta);
        else level.Decrease(-delta, level.Product!.Sku);
    }

    private async Task SaveWithConcurrencyAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Stock changed while you were editing it. Reload and try again.");
        }
    }
}
