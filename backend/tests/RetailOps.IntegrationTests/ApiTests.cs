using System.Net;
using System.Net.Http.Json;
using RetailOps.Application.Auth;
using RetailOps.Application.Catalog;
using RetailOps.Application.Common;
using RetailOps.Application.Inventory;
using RetailOps.Application.Sales;
using RetailOps.Application.Transfers;
using RetailOps.Domain.Enums;

namespace RetailOps.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AuthTests(ApiFactory api)
{
    [Fact]
    public async Task Login_with_valid_credentials_returns_token_and_profile()
    {
        var me = await (await api.LoginAsync(Users.ManagerLaPaz)).GetFromJsonAsync<UserProfile>("/api/auth/me", ApiFactory.Json);

        Assert.Equal("Manager", me!.Role);
        Assert.Equal("La Paz Centro", me.StoreName);
        Assert.Equal("Andes Retail", me.TenantName);
    }

    [Fact]
    public async Task Login_with_wrong_password_is_unauthorized()
    {
        var response = await api.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(Users.Admin, "nope"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoints_require_a_token()
    {
        var response = await api.CreateClient().GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

[Collection(ApiCollection.Name)]
public class TenantIsolationTests(ApiFactory api)
{
    [Fact]
    public async Task Each_tenant_only_sees_its_own_data()
    {
        var andes = await api.LoginAsync(Users.Admin);
        var altiplano = await api.LoginAsync(Users.OtherTenantAdmin);

        var andesStores = await andes.GetFromJsonAsync<List<StoreDto>>("/api/stores", ApiFactory.Json);
        var altiplanoStores = await altiplano.GetFromJsonAsync<List<StoreDto>>("/api/stores", ApiFactory.Json);

        Assert.Equal(4, andesStores!.Count);
        Assert.Equal("ORU-01", Assert.Single(altiplanoStores!).Code);
    }

    [Fact]
    public async Task Reading_another_tenants_resource_by_id_returns_404()
    {
        var andes = await api.LoginAsync(Users.Admin);
        var product = (await andes.GetFromJsonAsync<PagedResult<ProductDto>>("/api/products?pageSize=1", ApiFactory.Json))!.Items[0];

        var altiplano = await api.LoginAsync(Users.OtherTenantAdmin);
        var response = await altiplano.GetAsync($"/api/products/{product.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

[Collection(ApiCollection.Name)]
public class SalesTests(ApiFactory api)
{
    private static async Task<(Guid StoreId, StockItemDto Item)> PickStockAsync(HttpClient client, int minQuantity = 5)
    {
        var me = (await client.GetFromJsonAsync<UserProfile>("/api/auth/me", ApiFactory.Json))!;
        var stock = (await client.GetFromJsonAsync<PagedResult<StockItemDto>>($"/api/stores/{me.StoreId}/stock?pageSize=100", ApiFactory.Json))!;
        return (me.StoreId!.Value, stock.Items.First(i => i.Quantity >= minQuantity));
    }

    [Fact]
    public async Task Sale_decrements_stock_and_writes_ledger()
    {
        var cashier = await api.LoginAsync(Users.CashierLaPaz);
        var (storeId, item) = await PickStockAsync(cashier);

        var response = await cashier.PostJsonAsync("/api/sales",
            new CreateSaleRequest(storeId, PaymentMethod.Cash, [new CreateSaleLine(item.ProductId, 2)]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var sale = await response.ReadAsync<SaleDetailDto>();
        Assert.Equal(item.Price * 2, sale.Total);

        var after = await cashier.GetFromJsonAsync<StockItemDto>($"/api/stores/{storeId}/stock/barcode/{item.Barcode}", ApiFactory.Json);
        Assert.Equal(item.Quantity - 2, after!.Quantity);

        var movements = await cashier.GetFromJsonAsync<PagedResult<StockMovementDto>>(
            $"/api/stores/{storeId}/movements?productId={item.ProductId}&pageSize=5", ApiFactory.Json);
        Assert.Contains(movements!.Items, m => m.Reference == sale.Number && m.Quantity == -2);
    }

    [Fact]
    public async Task Overselling_is_rejected_with_422_and_stock_is_untouched()
    {
        var cashier = await api.LoginAsync(Users.CashierLaPaz);
        var (storeId, item) = await PickStockAsync(cashier);

        var response = await cashier.PostJsonAsync("/api/sales",
            new CreateSaleRequest(storeId, PaymentMethod.Card, [new CreateSaleLine(item.ProductId, item.Quantity + 1)]));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var after = await cashier.GetFromJsonAsync<StockItemDto>($"/api/stores/{storeId}/stock/barcode/{item.Barcode}", ApiFactory.Json);
        Assert.Equal(item.Quantity, after!.Quantity);
    }

    [Fact]
    public async Task Cashier_cannot_sell_in_another_store()
    {
        var cashier = await api.LoginAsync(Users.CashierLaPaz);
        var (_, item) = await PickStockAsync(cashier);
        var stores = await cashier.GetFromJsonAsync<List<StoreDto>>("/api/stores", ApiFactory.Json);
        var otherStore = stores!.First(s => s.Code == "SCZ-01");

        var response = await cashier.PostJsonAsync("/api/sales",
            new CreateSaleRequest(otherStore.Id, PaymentMethod.Cash, [new CreateSaleLine(item.ProductId, 1)]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Concurrent_sales_of_the_last_unit_never_oversell()
    {
        var manager = await api.LoginAsync(Users.ManagerLaPaz);
        var (storeId, item) = await PickStockAsync(manager, minQuantity: 2);

        // Bring the product down to exactly one unit through a physical count.
        var count = await manager.PostJsonAsync($"/api/stores/{storeId}/stock/counts",
            new StockCountRequest(Guid.NewGuid(), [new StockCountLine(item.ProductId, 1)]));
        count.EnsureSuccessStatusCode();

        var cashiers = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => api.LoginAsync(Users.CashierLaPaz)));
        var responses = await Task.WhenAll(cashiers.Select(c => c.PostJsonAsync("/api/sales",
            new CreateSaleRequest(storeId, PaymentMethod.Cash, [new CreateSaleLine(item.ProductId, 1)]))));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created),
            r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity }));

        var after = await manager.GetFromJsonAsync<StockItemDto>($"/api/stores/{storeId}/stock/barcode/{item.Barcode}", ApiFactory.Json);
        Assert.Equal(0, after!.Quantity);
    }
}

[Collection(ApiCollection.Name)]
public class AuthorizationTests(ApiFactory api)
{
    [Fact]
    public async Task Cashier_cannot_create_products()
    {
        var cashier = await api.LoginAsync(Users.CashierLaPaz);

        var response = await cashier.PostJsonAsync("/api/products",
            new UpsertProductRequest("NEW-001", "7779999999990", "Nuevo", "Abarrotes", 10, 5));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Manager_creates_product_and_it_appears_in_every_store_with_zero_stock()
    {
        var manager = await api.LoginAsync(Users.ManagerLaPaz);

        var response = await manager.PostJsonAsync("/api/products",
            new UpsertProductRequest("TST-777", "7779999999983", "Producto de prueba", "Abarrotes", 10, 5));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = await response.ReadAsync<ProductDto>();

        var me = (await manager.GetFromJsonAsync<UserProfile>("/api/auth/me", ApiFactory.Json))!;
        var stock = await manager.GetFromJsonAsync<StockItemDto>($"/api/stores/{me.StoreId}/stock/barcode/{product.Barcode}", ApiFactory.Json);
        Assert.Equal(0, stock!.Quantity);
    }

    [Fact]
    public async Task Invalid_payload_returns_validation_problem()
    {
        var manager = await api.LoginAsync(Users.ManagerLaPaz);

        var response = await manager.PostJsonAsync("/api/products",
            new UpsertProductRequest("", "123", "", "", 5, 10));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("barcode", body);
    }
}

[Collection(ApiCollection.Name)]
public class TransferAndCountTests(ApiFactory api)
{
    [Fact]
    public async Task Transfer_moves_stock_only_when_shipped_and_received()
    {
        var lpz = await api.LoginAsync(Users.ManagerLaPaz);
        var eal = await api.LoginAsync(Users.ManagerElAlto);
        var lpzStore = (await lpz.GetFromJsonAsync<UserProfile>("/api/auth/me", ApiFactory.Json))!.StoreId!.Value;
        var ealStore = (await eal.GetFromJsonAsync<UserProfile>("/api/auth/me", ApiFactory.Json))!.StoreId!.Value;

        var origin = (await lpz.GetFromJsonAsync<PagedResult<StockItemDto>>($"/api/stores/{lpzStore}/stock?pageSize=100", ApiFactory.Json))!
            .Items.First(i => i.Quantity >= 10);
        var destinationBefore = (await eal.GetFromJsonAsync<StockItemDto>($"/api/stores/{ealStore}/stock/barcode/{origin.Barcode}", ApiFactory.Json))!.Quantity;

        var created = await (await eal.PostJsonAsync("/api/transfers",
            new CreateTransferRequest(lpzStore, ealStore, "test", [new TransferLineRequest(origin.ProductId, 5)]))).ReadAsync<TransferDto>();
        Assert.Equal("Requested", created.Status);

        // Destination manager cannot dispatch from the origin store.
        Assert.Equal(HttpStatusCode.Forbidden, (await eal.PostAsync($"/api/transfers/{created.Id}/ship", null)).StatusCode);

        var shipped = await (await lpz.PostAsync($"/api/transfers/{created.Id}/ship", null)).ReadAsync<TransferDto>();
        Assert.Equal("InTransit", shipped.Status);
        var originAfterShip = await lpz.GetFromJsonAsync<StockItemDto>($"/api/stores/{lpzStore}/stock/barcode/{origin.Barcode}", ApiFactory.Json);
        Assert.Equal(origin.Quantity - 5, originAfterShip!.Quantity);

        var received = await (await eal.PostAsync($"/api/transfers/{created.Id}/receive", null)).ReadAsync<TransferDto>();
        Assert.Equal("Received", received.Status);
        var destinationAfter = await eal.GetFromJsonAsync<StockItemDto>($"/api/stores/{ealStore}/stock/barcode/{origin.Barcode}", ApiFactory.Json);
        Assert.Equal(destinationBefore + 5, destinationAfter!.Quantity);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await lpz.PostAsync($"/api/transfers/{created.Id}/cancel", null)).StatusCode);
    }

    [Fact]
    public async Task Stock_count_is_idempotent_per_client_count_id()
    {
        var manager = await api.LoginAsync(Users.ManagerElAlto);
        var storeId = (await manager.GetFromJsonAsync<UserProfile>("/api/auth/me", ApiFactory.Json))!.StoreId!.Value;
        var item = (await manager.GetFromJsonAsync<PagedResult<StockItemDto>>($"/api/stores/{storeId}/stock?pageSize=100", ApiFactory.Json))!.Items[3];
        var request = new StockCountRequest(Guid.NewGuid(), [new StockCountLine(item.ProductId, item.Quantity + 7)]);

        var first = await (await manager.PostJsonAsync($"/api/stores/{storeId}/stock/counts", request)).ReadAsync<StockCountResult>();
        var retry = await (await manager.PostJsonAsync($"/api/stores/{storeId}/stock/counts", request)).ReadAsync<StockCountResult>();

        Assert.False(first.AlreadyProcessed);
        Assert.Equal(1, first.AdjustmentsMade);
        Assert.True(retry.AlreadyProcessed);
        var after = await manager.GetFromJsonAsync<StockItemDto>($"/api/stores/{storeId}/stock/barcode/{item.Barcode}", ApiFactory.Json);
        Assert.Equal(item.Quantity + 7, after!.Quantity);
    }
}
