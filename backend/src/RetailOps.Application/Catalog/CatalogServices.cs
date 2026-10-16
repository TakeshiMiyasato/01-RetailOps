using FluentValidation;
using Microsoft.EntityFrameworkCore;
using RetailOps.Application.Common;
using RetailOps.Domain.Entities;

namespace RetailOps.Application.Catalog;

public sealed record StoreDto(Guid Id, string Code, string Name, string City, bool IsActive);

public sealed record CreateStoreRequest(string Code, string Name, string City);

public sealed class CreateStoreRequestValidator : AbstractValidator<CreateStoreRequest>
{
    public CreateStoreRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(16).Matches("^[A-Z0-9-]+$")
            .WithMessage("Code must be uppercase letters, digits or dashes.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.City).NotEmpty().MaximumLength(80);
    }
}

public sealed class StoreService(IAppDbContext db, IValidator<CreateStoreRequest> validator)
{
    public Task<List<StoreDto>> ListAsync(CancellationToken ct) =>
        db.Stores.OrderBy(s => s.Code)
            .Select(s => new StoreDto(s.Id, s.Code, s.Name, s.City, s.IsActive))
            .ToListAsync(ct);

    public async Task<StoreDto> CreateAsync(CreateStoreRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        if (await db.Stores.AnyAsync(s => s.Code == request.Code, ct))
            throw new ConflictException($"Store code '{request.Code}' already exists.");

        var store = new Store { Code = request.Code, Name = request.Name, City = request.City };
        db.Stores.Add(store);

        // Every new store starts with a zero stock row per active product so it shows up in inventory screens.
        var productIds = await db.Products.Where(p => p.IsActive).Select(p => p.Id).ToListAsync(ct);
        foreach (var productId in productIds)
            db.StockLevels.Add(StockLevel.Create(store.Id, productId));

        await db.SaveChangesAsync(ct);
        return new StoreDto(store.Id, store.Code, store.Name, store.City, store.IsActive);
    }
}

public sealed record ProductDto(Guid Id, string Sku, string Barcode, string Name, string Category, decimal Price, decimal Cost, bool IsActive);

public sealed record UpsertProductRequest(string Sku, string Barcode, string Name, string Category, decimal Price, decimal Cost, bool IsActive = true);

public sealed class UpsertProductRequestValidator : AbstractValidator<UpsertProductRequest>
{
    public UpsertProductRequestValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Barcode).NotEmpty().Matches("^[0-9]{8,14}$").WithMessage("Barcode must be 8 to 14 digits (EAN/UPC).");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0);
        RuleFor(x => x).Must(x => x.Price >= x.Cost).WithName("Price").WithMessage("Price cannot be lower than cost.");
    }
}

public sealed class ProductService(IAppDbContext db, IValidator<UpsertProductRequest> validator)
{
    private static readonly System.Linq.Expressions.Expression<Func<Product, ProductDto>> ToDto =
        p => new ProductDto(p.Id, p.Sku, p.Barcode, p.Name, p.Category, p.Price, p.Cost, p.IsActive);

    private static readonly Func<Product, ProductDto> MapProduct = ToDto.Compile();

    public Task<PagedResult<ProductDto>> SearchAsync(string? search, string? category, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search) || p.Sku.Contains(search) || p.Barcode == search);
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category == category);

        return query.OrderBy(p => p.Name).Select(ToDto).ToPagedAsync(page, pageSize, ct);
    }

    public Task<List<string>> CategoriesAsync(CancellationToken ct) =>
        db.Products.Select(p => p.Category).Distinct().OrderBy(c => c).ToListAsync(ct);

    public async Task<ProductDto> GetAsync(Guid id, CancellationToken ct) =>
        await db.Products.Where(p => p.Id == id).Select(ToDto).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Product", id);

    /// <summary>Used by the POS and the mobile scanner app.</summary>
    public async Task<ProductDto> GetByBarcodeAsync(string barcode, CancellationToken ct) =>
        await db.Products.Where(p => p.Barcode == barcode).Select(ToDto).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Product with barcode", barcode);

    public async Task<ProductDto> CreateAsync(UpsertProductRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        await EnsureUniqueAsync(request, null, ct);

        var product = new Product
        {
            Sku = request.Sku.Trim().ToUpperInvariant(),
            Barcode = request.Barcode,
            Name = request.Name.Trim(),
            Category = request.Category.Trim(),
            Price = request.Price,
            Cost = request.Cost,
            IsActive = request.IsActive
        };
        db.Products.Add(product);

        var storeIds = await db.Stores.Select(s => s.Id).ToListAsync(ct);
        foreach (var storeId in storeIds)
            db.StockLevels.Add(StockLevel.Create(storeId, product.Id));

        await db.SaveChangesAsync(ct);
        return MapProduct(product);
    }

    public async Task<ProductDto> UpdateAsync(Guid id, UpsertProductRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Product", id);
        await EnsureUniqueAsync(request, id, ct);

        product.Sku = request.Sku.Trim().ToUpperInvariant();
        product.Barcode = request.Barcode;
        product.Name = request.Name.Trim();
        product.Category = request.Category.Trim();
        product.Price = request.Price;
        product.Cost = request.Cost;
        product.IsActive = request.IsActive;

        await db.SaveChangesAsync(ct);
        return MapProduct(product);
    }

    private async Task EnsureUniqueAsync(UpsertProductRequest request, Guid? excludeId, CancellationToken ct)
    {
        var sku = request.Sku.Trim().ToUpperInvariant();
        if (await db.Products.AnyAsync(p => p.Id != excludeId && (p.Sku == sku || p.Barcode == request.Barcode), ct))
            throw new ConflictException("Another product already uses this SKU or barcode.");
    }

}

