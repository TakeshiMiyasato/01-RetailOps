using FluentValidation;
using Microsoft.EntityFrameworkCore;
using RetailOps.Application.Common;
using RetailOps.Domain.Entities;
using RetailOps.Domain.Enums;

namespace RetailOps.Application.Transfers;

public sealed record CreateTransferRequest(Guid FromStoreId, Guid ToStoreId, string? Note, IReadOnlyList<TransferLineRequest> Lines);

public sealed record TransferLineRequest(Guid ProductId, int Quantity);

public sealed record TransferLineDto(Guid ProductId, string Sku, string ProductName, int Quantity);

public sealed record TransferDto(
    Guid Id, string Number, Guid FromStoreId, string FromStore, Guid ToStoreId, string ToStore, string Status,
    DateTimeOffset RequestedAt, DateTimeOffset? ShippedAt, DateTimeOffset? ReceivedAt, string? Note,
    int TotalUnits, IReadOnlyList<TransferLineDto> Lines);

public sealed class CreateTransferRequestValidator : AbstractValidator<CreateTransferRequest>
{
    public CreateTransferRequestValidator()
    {
        RuleFor(x => x.FromStoreId).NotEmpty();
        RuleFor(x => x.ToStoreId).NotEmpty().NotEqual(x => x.FromStoreId).WithMessage("Destination must differ from origin.");
        RuleFor(x => x.Note).MaximumLength(300);
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty();
            line.RuleFor(l => l.Quantity).InclusiveBetween(1, 10_000);
        });
    }
}

public sealed class TransferService(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock, IValidator<CreateTransferRequest> validator)
{
    public async Task<TransferDto> CreateAsync(CreateTransferRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        EnsureInvolved(request.FromStoreId, request.ToStoreId);

        var stores = await db.Stores.Where(s => s.Id == request.FromStoreId || s.Id == request.ToStoreId).ToListAsync(ct);
        if (stores.Count != 2) throw new NotFoundException("Store", request.FromStoreId);
        var from = stores.Single(s => s.Id == request.FromStoreId);

        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        var transfer = Transfer.Create(DocumentNumber.Next("T", from.Code, clock.GetUtcNow()),
            request.FromStoreId, request.ToStoreId, currentUser.UserId, request.Note);
        transfer.RequestedAt = clock.GetUtcNow();

        foreach (var line in request.Lines)
        {
            var product = products.GetValueOrDefault(line.ProductId) ?? throw new NotFoundException("Product", line.ProductId);
            transfer.AddLine(product.Id, product.Sku, line.Quantity);
        }

        db.Transfers.Add(transfer);
        await db.SaveChangesAsync(ct);
        return await GetAsync(transfer.Id, ct);
    }

    /// <summary>Origin store dispatches the goods: stock leaves the origin now, not when requested.</summary>
    public async Task<TransferDto> ShipAsync(Guid id, CancellationToken ct)
    {
        var transfer = await LoadAsync(id, ct);
        currentUser.EnsureCanOperate(transfer.FromStoreId);
        var now = clock.GetUtcNow();
        transfer.MarkShipped(now);

        var levels = await LevelsAsync(transfer.FromStoreId, transfer, ct);
        foreach (var line in transfer.Lines)
        {
            levels[line.ProductId].Decrease(line.Quantity, line.Sku);
            AddMovement(transfer.FromStoreId, line, -line.Quantity, MovementType.TransferOut, transfer.Number, now);
        }

        await SaveAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<TransferDto> ReceiveAsync(Guid id, CancellationToken ct)
    {
        var transfer = await LoadAsync(id, ct);
        currentUser.EnsureCanOperate(transfer.ToStoreId);
        var now = clock.GetUtcNow();
        transfer.MarkReceived(now);

        var levels = await LevelsAsync(transfer.ToStoreId, transfer, ct);
        foreach (var line in transfer.Lines)
        {
            if (!levels.TryGetValue(line.ProductId, out var level))
            {
                level = StockLevel.Create(transfer.ToStoreId, line.ProductId);
                db.StockLevels.Add(level);
            }
            level.Increase(line.Quantity);
            AddMovement(transfer.ToStoreId, line, line.Quantity, MovementType.TransferIn, transfer.Number, now);
        }

        await SaveAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<TransferDto> CancelAsync(Guid id, CancellationToken ct)
    {
        var transfer = await LoadAsync(id, ct);
        EnsureInvolved(transfer.FromStoreId, transfer.ToStoreId);
        transfer.Cancel();
        await SaveAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<PagedResult<TransferDto>> ListAsync(TransferStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Transfers.AsNoTracking();
        if (currentUser.Role != UserRole.Admin)
            query = query.Where(t => t.FromStoreId == currentUser.StoreId || t.ToStoreId == currentUser.StoreId);
        if (status is not null)
            query = query.Where(t => t.Status == status);
        return await Projected(query.OrderByDescending(t => t.RequestedAt)).ToPagedAsync(page, pageSize, ct);
    }

    public async Task<TransferDto> GetAsync(Guid id, CancellationToken ct)
    {
        var dto = await Projected(db.Transfers.AsNoTracking().Where(t => t.Id == id)).FirstOrDefaultAsync(ct)
                  ?? throw new NotFoundException("Transfer", id);
        EnsureInvolved(dto.FromStoreId, dto.ToStoreId);
        return dto;
    }

    private static IQueryable<TransferDto> Projected(IQueryable<Transfer> source) =>
        source.Select(t => new TransferDto(
            t.Id, t.Number, t.FromStoreId, t.FromStore!.Name, t.ToStoreId, t.ToStore!.Name, t.Status.ToString(),
            t.RequestedAt, t.ShippedAt, t.ReceivedAt, t.Note,
            t.Lines.Sum(l => l.Quantity),
            t.Lines.Select(l => new TransferLineDto(l.ProductId, l.Sku, l.Product!.Name, l.Quantity)).ToList()));

    private async Task<Transfer> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Transfers.Include(t => t.Lines).FirstOrDefaultAsync(t => t.Id == id, ct)
        ?? throw new NotFoundException("Transfer", id);

    private Task<Dictionary<Guid, StockLevel>> LevelsAsync(Guid storeId, Transfer transfer, CancellationToken ct)
    {
        var ids = transfer.Lines.Select(l => l.ProductId).ToList();
        return db.StockLevels.Where(s => s.StoreId == storeId && ids.Contains(s.ProductId)).ToDictionaryAsync(s => s.ProductId, ct);
    }

    private void AddMovement(Guid storeId, TransferLine line, int quantity, MovementType type, string reference, DateTimeOffset at) =>
        db.StockMovements.Add(new StockMovement
        {
            StoreId = storeId, ProductId = line.ProductId, Quantity = quantity, Type = type,
            Reference = reference, UserId = currentUser.UserId, CreatedAt = at
        });

    private void EnsureInvolved(Guid fromStoreId, Guid toStoreId)
    {
        if (currentUser.Role == UserRole.Admin) return;
        if (currentUser.StoreId != fromStoreId && currentUser.StoreId != toStoreId)
            throw new ForbiddenException("You can only manage transfers that involve your store.");
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The transfer or its stock changed concurrently. Reload and try again.");
        }
    }
}
