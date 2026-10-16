using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RetailOps.Application.Auth;
using RetailOps.Application.Catalog;
using RetailOps.Application.Common;
using RetailOps.Application.Inventory;
using RetailOps.Application.Reports;
using RetailOps.Application.Sales;
using RetailOps.Application.Transfers;
using RetailOps.Domain.Enums;

namespace RetailOps.Api.Controllers;

public static class Policies
{
    public const string Admin = "admin";
    public const string ManagerOrAdmin = "manager-or-admin";
}

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public Task<LoginResponse> Login(LoginRequest request, CancellationToken ct) => auth.LoginAsync(request, ct);

    [HttpGet("me")]
    [Authorize]
    public Task<UserProfile> Me(CancellationToken ct) => auth.MeAsync(ct);
}

[ApiController]
[Authorize]
[Route("api/stores")]
public sealed class StoresController(StoreService stores, InventoryService inventory) : ControllerBase
{
    [HttpGet]
    public Task<List<StoreDto>> List(CancellationToken ct) => stores.ListAsync(ct);

    [HttpPost]
    [Authorize(Policies.Admin)]
    public async Task<ActionResult<StoreDto>> Create(CreateStoreRequest request, CancellationToken ct)
    {
        var store = await stores.CreateAsync(request, ct);
        return Created($"/api/stores/{store.Id}", store);
    }

    [HttpGet("{storeId:guid}/stock")]
    public Task<PagedResult<StockItemDto>> Stock(Guid storeId, string? search, bool lowOnly = false, int page = 1, int pageSize = 25, CancellationToken ct = default) =>
        inventory.GetStockAsync(storeId, search, lowOnly, page, pageSize, ct);

    [HttpGet("{storeId:guid}/stock/barcode/{barcode}")]
    public Task<StockItemDto> StockByBarcode(Guid storeId, string barcode, CancellationToken ct) =>
        inventory.GetByBarcodeAsync(storeId, barcode, ct);

    [HttpPost("{storeId:guid}/stock/adjustments")]
    [Authorize(Policies.ManagerOrAdmin)]
    public Task<StockItemDto> Adjust(Guid storeId, StockAdjustmentRequest request, CancellationToken ct) =>
        inventory.AdjustAsync(storeId, request, ct);

    /// <summary>Idempotent: re-sending the same clientCountId returns alreadyProcessed=true without touching stock.</summary>
    [HttpPost("{storeId:guid}/stock/counts")]
    public Task<StockCountResult> SubmitCount(Guid storeId, StockCountRequest request, CancellationToken ct) =>
        inventory.SubmitCountAsync(storeId, request, ct);

    [HttpGet("{storeId:guid}/movements")]
    public Task<PagedResult<StockMovementDto>> Movements(Guid storeId, Guid? productId, int page = 1, int pageSize = 25, CancellationToken ct = default) =>
        inventory.GetMovementsAsync(storeId, productId, page, pageSize, ct);
}

[ApiController]
[Authorize]
[Route("api/products")]
public sealed class ProductsController(ProductService products) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ProductDto>> Search(string? search, string? category, int page = 1, int pageSize = 25, CancellationToken ct = default) =>
        products.SearchAsync(search, category, page, pageSize, ct);

    [HttpGet("categories")]
    public Task<List<string>> Categories(CancellationToken ct) => products.CategoriesAsync(ct);

    [HttpGet("{id:guid}")]
    public Task<ProductDto> Get(Guid id, CancellationToken ct) => products.GetAsync(id, ct);

    [HttpGet("barcode/{barcode}")]
    public Task<ProductDto> ByBarcode(string barcode, CancellationToken ct) => products.GetByBarcodeAsync(barcode, ct);

    [HttpPost]
    [Authorize(Policies.ManagerOrAdmin)]
    public async Task<ActionResult<ProductDto>> Create(UpsertProductRequest request, CancellationToken ct)
    {
        var product = await products.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policies.ManagerOrAdmin)]
    public Task<ProductDto> Update(Guid id, UpsertProductRequest request, CancellationToken ct) => products.UpdateAsync(id, request, ct);
}

[ApiController]
[Authorize]
[Route("api/sales")]
public sealed class SalesController(SaleService sales) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SaleDetailDto>> Create(CreateSaleRequest request, CancellationToken ct)
    {
        var sale = await sales.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = sale.Id }, sale);
    }

    [HttpGet]
    public Task<PagedResult<SaleSummaryDto>> List(Guid? storeId, DateTimeOffset? from, DateTimeOffset? to, int page = 1, int pageSize = 25, CancellationToken ct = default) =>
        sales.ListAsync(storeId, from, to, page, pageSize, ct);

    [HttpGet("{id:guid}")]
    public Task<SaleDetailDto> Get(Guid id, CancellationToken ct) => sales.GetAsync(id, ct);
}

[ApiController]
[Authorize(Policies.ManagerOrAdmin)]
[Route("api/transfers")]
public sealed class TransfersController(TransferService transfers) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<TransferDto>> List(TransferStatus? status, int page = 1, int pageSize = 25, CancellationToken ct = default) =>
        transfers.ListAsync(status, page, pageSize, ct);

    [HttpGet("{id:guid}")]
    public Task<TransferDto> Get(Guid id, CancellationToken ct) => transfers.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<TransferDto>> Create(CreateTransferRequest request, CancellationToken ct)
    {
        var transfer = await transfers.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = transfer.Id }, transfer);
    }

    [HttpPost("{id:guid}/ship")]
    public Task<TransferDto> Ship(Guid id, CancellationToken ct) => transfers.ShipAsync(id, ct);

    [HttpPost("{id:guid}/receive")]
    public Task<TransferDto> Receive(Guid id, CancellationToken ct) => transfers.ReceiveAsync(id, ct);

    [HttpPost("{id:guid}/cancel")]
    public Task<TransferDto> Cancel(Guid id, CancellationToken ct) => transfers.CancelAsync(id, ct);
}

[ApiController]
[Authorize]
[Route("api/reports")]
public sealed class ReportsController(ReportService reports) : ControllerBase
{
    [HttpGet("dashboard")]
    public Task<DashboardDto> Dashboard(Guid? storeId, int days = 30, CancellationToken ct = default) =>
        reports.GetDashboardAsync(storeId, days, ct);
}
