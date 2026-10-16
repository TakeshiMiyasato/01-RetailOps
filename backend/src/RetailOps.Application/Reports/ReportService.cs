using Microsoft.EntityFrameworkCore;
using RetailOps.Application.Common;
using RetailOps.Domain.Enums;

namespace RetailOps.Application.Reports;

public sealed record DashboardDto(
    decimal RevenueToday,
    decimal RevenuePeriod,
    int SalesPeriod,
    decimal AverageTicket,
    decimal GrossMarginPct,
    int LowStockItems,
    int OpenTransfers,
    IReadOnlyList<DailyRevenuePoint> DailyRevenue,
    IReadOnlyList<StoreRevenue> RevenueByStore,
    IReadOnlyList<TopProduct> TopProducts);

public sealed record DailyRevenuePoint(DateOnly Date, decimal Revenue, int Sales);

public sealed record StoreRevenue(Guid StoreId, string StoreName, decimal Revenue, int Sales);

public sealed record TopProduct(string Sku, string Name, int Units, decimal Revenue);

public sealed class ReportService(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
{
    // Bolivia is UTC-4 all year (no DST), so business days are bucketed with a fixed offset.
    private static readonly TimeSpan BusinessOffset = TimeSpan.FromHours(-4);

    public async Task<DashboardDto> GetDashboardAsync(Guid? storeId, int days, CancellationToken ct)
    {
        days = Math.Clamp(days, 1, 365);
        var scopedStore = currentUser.ScopeStore(storeId);

        var localNow = clock.GetUtcNow().ToOffset(BusinessOffset);
        var todayStart = new DateTimeOffset(localNow.Date, BusinessOffset);
        var periodStart = todayStart.AddDays(-(days - 1));

        var sales = db.Sales.AsNoTracking().Where(s => s.CreatedAt >= periodStart);
        if (scopedStore is not null) sales = sales.Where(s => s.StoreId == scopedStore);

        // Daily buckets are computed in memory: the row count per period is small and it keeps the offset logic in C#.
        var rows = await sales.Select(s => new { s.CreatedAt, s.Total, s.StoreId }).ToListAsync(ct);

        var daily = Enumerable.Range(0, days)
            .Select(i => DateOnly.FromDateTime(periodStart.AddDays(i).Date))
            .GroupJoin(rows, d => d, r => DateOnly.FromDateTime(r.CreatedAt.ToOffset(BusinessOffset).Date),
                (d, rs) => new DailyRevenuePoint(d, rs.Sum(r => r.Total), rs.Count()))
            .ToList();

        var storeNames = await db.Stores.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var byStore = rows.GroupBy(r => r.StoreId)
            .Select(g => new StoreRevenue(g.Key, storeNames.GetValueOrDefault(g.Key, "?"), g.Sum(r => r.Total), g.Count()))
            .OrderByDescending(s => s.Revenue)
            .ToList();

        var lines = sales.SelectMany(s => s.Lines);
        var topProducts = (await lines
                .GroupBy(l => new { l.Sku, l.ProductName })
                .Select(g => new { g.Key.Sku, g.Key.ProductName, Units = g.Sum(l => l.Quantity), Revenue = g.Sum(l => l.UnitPrice * l.Quantity) })
                .OrderByDescending(p => p.Revenue)
                .Take(8)
                .ToListAsync(ct))
            .Select(p => new TopProduct(p.Sku, p.ProductName, p.Units, Math.Round(p.Revenue, 2)))
            .ToList();

        var costOfGoods = await lines
            .Join(db.Products, l => l.ProductId, p => p.Id, (l, p) => p.Cost * l.Quantity)
            .SumAsync(ct);

        var stock = db.StockLevels.AsNoTracking().Where(s => s.Product!.IsActive && s.Quantity <= s.ReorderPoint);
        if (scopedStore is not null) stock = stock.Where(s => s.StoreId == scopedStore);

        var transfers = db.Transfers.AsNoTracking().Where(t => t.Status == TransferStatus.Requested || t.Status == TransferStatus.InTransit);
        if (scopedStore is not null) transfers = transfers.Where(t => t.FromStoreId == scopedStore || t.ToStoreId == scopedStore);

        var revenue = rows.Sum(r => r.Total);
        return new DashboardDto(
            RevenueToday: rows.Where(r => r.CreatedAt >= todayStart).Sum(r => r.Total),
            RevenuePeriod: revenue,
            SalesPeriod: rows.Count,
            AverageTicket: rows.Count == 0 ? 0 : Math.Round(revenue / rows.Count, 2),
            GrossMarginPct: revenue == 0 ? 0 : Math.Round((revenue - costOfGoods) / revenue * 100, 1),
            LowStockItems: await stock.CountAsync(ct),
            OpenTransfers: await transfers.CountAsync(ct),
            DailyRevenue: daily,
            RevenueByStore: byStore,
            TopProducts: topProducts);
    }
}
