using FluentValidation;
using Microsoft.EntityFrameworkCore;
using RetailOps.Application.Common;
using RetailOps.Domain.Entities;
using RetailOps.Domain.Enums;

namespace RetailOps.Application.Sales;

public sealed record CreateSaleRequest(Guid StoreId, PaymentMethod PaymentMethod, IReadOnlyList<CreateSaleLine> Lines);

public sealed record CreateSaleLine(Guid ProductId, int Quantity);

public sealed record SaleSummaryDto(Guid Id, string Number, string StoreName, string CashierName, string PaymentMethod, decimal Total, int Items, DateTimeOffset CreatedAt);

public sealed record SaleLineDto(Guid ProductId, string Sku, string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal);

public sealed record SaleDetailDto(Guid Id, string Number, Guid StoreId, string StoreName, string CashierName, string PaymentMethod, decimal Total, DateTimeOffset CreatedAt, IReadOnlyList<SaleLineDto> Lines);

public sealed class CreateSaleRequestValidator : AbstractValidator<CreateSaleRequest>
{
    public CreateSaleRequestValidator()
    {
        RuleFor(x => x.StoreId).NotEmpty();
        RuleFor(x => x.PaymentMethod).IsInEnum();
        RuleFor(x => x.Lines).NotEmpty().Must(l => l.Count <= 100).WithMessage("A sale cannot have more than 100 lines.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty();
            line.RuleFor(l => l.Quantity).InclusiveBetween(1, 999);
        });
    }
}

public sealed class SaleService(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock, IValidator<CreateSaleRequest> validator)
{
    /// <summary>
    /// Registers a sale and decrements stock in a single SaveChanges (one transaction).
    /// If two cashiers sell the last unit at the same time, the StockLevel row version makes one of them fail with 409.
    /// </summary>
    public async Task<SaleDetailDto> CreateAsync(CreateSaleRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        currentUser.EnsureCanOperate(request.StoreId);

        var store = await db.Stores.FirstOrDefaultAsync(s => s.Id == request.StoreId && s.IsActive, ct)
                    ?? throw new NotFoundException("Store", request.StoreId);

        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var levels = await db.StockLevels.Where(s => s.StoreId == store.Id && productIds.Contains(s.ProductId))
            .ToDictionaryAsync(s => s.ProductId, ct);

        var now = clock.GetUtcNow();
        var sale = new Sale
        {
            Number = DocumentNumber.Next("V", store.Code, now),
            StoreId = store.Id,
            CashierId = currentUser.UserId,
            PaymentMethod = request.PaymentMethod,
            CreatedAt = now
        };

        foreach (var line in request.Lines)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
                throw new NotFoundException("Product", line.ProductId);
            if (!levels.TryGetValue(line.ProductId, out var level))
                throw new NotFoundException("Stock level", line.ProductId);

            sale.AddLine(product, line.Quantity);
            level.Decrease(line.Quantity, product.Sku);
            db.StockMovements.Add(new StockMovement
            {
                StoreId = store.Id,
                ProductId = product.Id,
                Quantity = -line.Quantity,
                Type = MovementType.Sale,
                Reference = sale.Number,
                UserId = currentUser.UserId,
                CreatedAt = now
            });
        }

        sale.EnsureNotEmpty();
        db.Sales.Add(sale);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Stock changed during checkout. Please retry the sale.");
        }

        return await GetAsync(sale.Id, ct);
    }

    public Task<PagedResult<SaleSummaryDto>> ListAsync(Guid? storeId, DateTimeOffset? from, DateTimeOffset? to, int page, int pageSize, CancellationToken ct)
    {
        var scopedStore = currentUser.ScopeStore(storeId);
        var query = db.Sales.AsNoTracking();
        if (scopedStore is not null) query = query.Where(s => s.StoreId == scopedStore);
        if (from is not null) query = query.Where(s => s.CreatedAt >= from);
        if (to is not null) query = query.Where(s => s.CreatedAt < to);

        return query.OrderByDescending(s => s.CreatedAt)
            .Select(s => new SaleSummaryDto(s.Id, s.Number, s.Store!.Name, s.Cashier!.FullName, s.PaymentMethod.ToString(),
                s.Total, s.Lines.Sum(l => l.Quantity), s.CreatedAt))
            .ToPagedAsync(page, pageSize, ct);
    }

    public async Task<SaleDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        var sale = await db.Sales.AsNoTracking()
                       .Include(s => s.Lines).Include(s => s.Store).Include(s => s.Cashier)
                       .FirstOrDefaultAsync(s => s.Id == id, ct)
                   ?? throw new NotFoundException("Sale", id);
        currentUser.EnsureCanOperate(sale.StoreId);

        return new SaleDetailDto(sale.Id, sale.Number, sale.StoreId, sale.Store!.Name, sale.Cashier!.FullName,
            sale.PaymentMethod.ToString(), sale.Total, sale.CreatedAt,
            sale.Lines.Select(l => new SaleLineDto(l.ProductId, l.Sku, l.ProductName, l.Quantity, l.UnitPrice, l.LineTotal)).ToList());
    }
}
