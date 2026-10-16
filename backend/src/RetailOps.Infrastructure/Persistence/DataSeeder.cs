using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RetailOps.Domain.Entities;
using RetailOps.Domain.Enums;

namespace RetailOps.Infrastructure.Persistence;

/// <summary>
/// Seeds two tenants so the demo can prove isolation: "Andes Retail" (4 stores, ~45 days of sales history)
/// and a small "Altiplano Market". Deterministic (fixed Random seed) so screenshots and tests are reproducible.
/// </summary>
public sealed class DataSeeder(AppDbContext db, IPasswordHasher<User> hasher, TimeProvider clock, ILogger<DataSeeder> logger)
{
    public const string DemoPassword = "Demo2026!";
    private const int HistoryDays = 45;
    private static readonly TimeSpan LocalOffset = TimeSpan.FromHours(-4);

    private static readonly (string Sku, string Name, string Category, decimal Price, decimal Cost, int Popularity)[] Catalog =
    [
        ("ABA-001", "Arroz grano largo 1 kg", "Abarrotes", 9.50m, 7.10m, 9),
        ("ABA-002", "Azúcar blanca 1 kg", "Abarrotes", 7.00m, 5.40m, 8),
        ("ABA-003", "Aceite vegetal 900 ml", "Abarrotes", 14.50m, 11.20m, 8),
        ("ABA-004", "Fideo tallarín 400 g", "Abarrotes", 6.00m, 4.20m, 7),
        ("ABA-005", "Harina de trigo 1 kg", "Abarrotes", 8.00m, 6.00m, 5),
        ("ABA-006", "Quinua real 500 g", "Abarrotes", 16.00m, 11.50m, 4),
        ("ABA-007", "Sal yodada 1 kg", "Abarrotes", 3.00m, 1.90m, 4),
        ("ABA-008", "Café molido 250 g", "Abarrotes", 28.00m, 19.00m, 3),
        ("LAC-001", "Leche entera 1 L", "Lácteos", 7.50m, 5.80m, 10),
        ("LAC-002", "Yogurt frutilla 1 L", "Lácteos", 13.00m, 9.40m, 6),
        ("LAC-003", "Queso fresco 500 g", "Lácteos", 32.00m, 24.00m, 4),
        ("LAC-004", "Mantequilla 200 g", "Lácteos", 15.00m, 11.00m, 4),
        ("BEB-001", "Agua mineral 2 L", "Bebidas", 6.50m, 4.00m, 9),
        ("BEB-002", "Gaseosa cola 2 L", "Bebidas", 12.00m, 8.50m, 9),
        ("BEB-003", "Jugo de durazno 1 L", "Bebidas", 10.00m, 7.20m, 5),
        ("BEB-004", "Cerveza rubia 620 ml", "Bebidas", 14.00m, 10.00m, 7),
        ("BEB-005", "Té de coca 25 sobres", "Bebidas", 9.00m, 5.50m, 3),
        ("BEB-006", "Bebida energética 473 ml", "Bebidas", 15.00m, 10.50m, 4),
        ("PAN-001", "Pan de molde blanco", "Panadería", 16.00m, 11.00m, 6),
        ("PAN-002", "Galletas de agua 300 g", "Panadería", 8.50m, 5.90m, 5),
        ("PAN-003", "Marraqueta (unidad)", "Panadería", 1.00m, 0.60m, 10),
        ("SNK-001", "Papas fritas 150 g", "Snacks", 11.00m, 7.30m, 6),
        ("SNK-002", "Chocolate con leche 100 g", "Snacks", 12.00m, 8.00m, 5),
        ("SNK-003", "Maní salado 200 g", "Snacks", 9.00m, 5.80m, 4),
        ("LIM-001", "Detergente en polvo 1 kg", "Limpieza", 24.00m, 17.50m, 4),
        ("LIM-002", "Lavavajillas líquido 750 ml", "Limpieza", 14.00m, 9.80m, 4),
        ("LIM-003", "Lavandina 1 L", "Limpieza", 8.00m, 5.20m, 4),
        ("LIM-004", "Papel higiénico x4", "Limpieza", 13.00m, 9.10m, 7),
        ("CPE-001", "Jabón de tocador 3 un.", "Cuidado personal", 12.00m, 8.10m, 4),
        ("CPE-002", "Shampoo 400 ml", "Cuidado personal", 26.00m, 18.00m, 3),
        ("CPE-003", "Pasta dental 90 g", "Cuidado personal", 11.00m, 7.40m, 4),
        ("CPE-004", "Desodorante aerosol", "Cuidado personal", 22.00m, 15.50m, 2),
        ("FRU-001", "Plátano (kg)", "Frutas y verduras", 6.00m, 3.80m, 7),
        ("FRU-002", "Papa holandesa (kg)", "Frutas y verduras", 5.00m, 3.20m, 8),
        ("FRU-003", "Tomate (kg)", "Frutas y verduras", 8.00m, 5.10m, 6),
        ("FRU-004", "Cebolla (kg)", "Frutas y verduras", 6.50m, 4.00m, 6),
    ];

    private static readonly (string Code, string Name, string City, double Traffic)[] AndesStores =
    [
        ("LPZ-01", "La Paz Centro", "La Paz", 1.0),
        ("EAL-01", "El Alto Ceja", "El Alto", 0.9),
        ("CBA-01", "Cochabamba Cala Cala", "Cochabamba", 0.75),
        ("SCZ-01", "Santa Cruz Equipetrol", "Santa Cruz", 1.15),
    ];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Tenants.AnyAsync(ct))
        {
            logger.LogInformation("Database already seeded, skipping.");
            return;
        }

        logger.LogInformation("Seeding demo tenants...");
        await SeedAndesRetailAsync(ct);
        await SeedAltiplanoMarketAsync(ct);
        logger.LogInformation("Seeding finished.");
    }

    private async Task SeedAndesRetailAsync(CancellationToken ct)
    {
        var random = new Random(2026);
        var tenant = new Tenant { Name = "Andes Retail", Slug = "andes-retail" };
        db.Tenants.Add(tenant);

        var stores = AndesStores.Select(s => new Store { TenantId = tenant.Id, Code = s.Code, Name = s.Name, City = s.City }).ToList();
        db.Stores.AddRange(stores);

        var admin = NewUser(tenant.Id, "admin@andesretail.demo", "Valeria Quispe", UserRole.Admin, null);
        var cashiers = new Dictionary<Guid, User>();
        var people = new[] { ("lpz", "Marco Mamani", "Lucía Condori"), ("eal", "Rosa Choque", "Iván Apaza"),
                             ("cba", "Diego Rojas", "Paola Vargas"), ("scz", "Andrea Suárez", "Carlos Justiniano") };
        for (var i = 0; i < stores.Count; i++)
        {
            var (code, manager, cashier) = people[i];
            NewUser(tenant.Id, $"gerente.{code}@andesretail.demo", manager, UserRole.Manager, stores[i].Id);
            cashiers[stores[i].Id] = NewUser(tenant.Id, $"cajero.{code}@andesretail.demo", cashier, UserRole.Cashier, stores[i].Id);
        }

        var products = Catalog.Select((p, i) => new Product
        {
            TenantId = tenant.Id, Sku = p.Sku, Barcode = Ean13("777", 100001 + i), Name = p.Name,
            Category = p.Category, Price = p.Price, Cost = p.Cost
        }).ToList();
        db.Products.AddRange(products);
        await db.SaveChangesAsync(ct);

        // Weighted pick list so popular items (bread, milk, water) dominate tickets like in a real store.
        var weighted = products.SelectMany((p, i) => Enumerable.Repeat(p, Catalog[i].Popularity)).ToArray();
        var today = clock.GetUtcNow().ToOffset(LocalOffset).Date;
        var sold = new Dictionary<(Guid Store, Guid Product), int>();

        for (var day = HistoryDays; day >= 1; day--)
        {
            var date = today.AddDays(-day);
            var weekendBoost = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 1.3 : 1.0;

            for (var s = 0; s < stores.Count; s++)
            {
                var store = stores[s];
                var count = (int)(random.Next(16, 28) * AndesStores[s].Traffic * weekendBoost);
                for (var n = 0; n < count; n++)
                {
                    var at = new DateTimeOffset(date.AddHours(8).AddMinutes(random.Next(0, 13 * 60)), LocalOffset);
                    var sale = new Sale
                    {
                        TenantId = tenant.Id,
                        Number = $"V-{store.Code}-{at:yyMMdd}-{n + 1:D4}",
                        StoreId = store.Id,
                        CashierId = cashiers[store.Id].Id,
                        PaymentMethod = random.NextDouble() switch { < 0.55 => PaymentMethod.Cash, < 0.8 => PaymentMethod.QrTransfer, _ => PaymentMethod.Card },
                        CreatedAt = at.ToUniversalTime()
                    };

                    var lineCount = random.Next(1, 6);
                    for (var l = 0; l < lineCount; l++)
                    {
                        var product = weighted[random.Next(weighted.Length)];
                        var qty = random.Next(1, 4);
                        sale.AddLine(product, qty);
                        sold[(store.Id, product.Id)] = sold.GetValueOrDefault((store.Id, product.Id)) + qty;
                        db.StockMovements.Add(new StockMovement
                        {
                            TenantId = tenant.Id, StoreId = store.Id, ProductId = product.Id, Quantity = -qty,
                            Type = MovementType.Sale, Reference = sale.Number, UserId = sale.CashierId, CreatedAt = sale.CreatedAt
                        });
                    }
                    db.Sales.Add(sale);
                }
            }
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }

        // Current stock is chosen first; the opening receipt is back-calculated so the ledger always reconciles.
        var openingDate = new DateTimeOffset(today.AddDays(-HistoryDays - 1).AddHours(7), LocalOffset).ToUniversalTime();
        foreach (var store in stores)
        foreach (var product in products)
        {
            var current = random.NextDouble() < 0.12 ? random.Next(0, 9) : random.Next(25, 160);
            var opening = current + sold.GetValueOrDefault((store.Id, product.Id));
            var level = StockLevel.Create(store.Id, product.Id, current, reorderPoint: 12);
            level.TenantId = tenant.Id;
            db.StockLevels.Add(level);
            db.StockMovements.Add(new StockMovement
            {
                TenantId = tenant.Id, StoreId = store.Id, ProductId = product.Id, Quantity = opening,
                Type = MovementType.Receipt, Reference = "OPENING", Note = "Opening balance", UserId = admin.Id, CreatedAt = openingDate
            });
        }
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        await SeedTransfersAsync(tenant.Id, stores, products, admin.Id, today, ct);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    /// <summary>One transfer per state so the transfers screen has something to act on in every column.</summary>
    private async Task SeedTransfersAsync(Guid tenantId, List<Store> stores, List<Product> products, Guid userId, DateTime today, CancellationToken ct)
    {
        var requestedAt = new DateTimeOffset(today.AddHours(9), LocalOffset).ToUniversalTime();

        // In transit: stock already left Santa Cruz, so it is decremented and logged exactly like TransferService.ShipAsync does.
        var inTransit = Transfer.Create($"T-{stores[3].Code}-SEED-003", stores[3].Id, stores[2].Id, userId, "Refuerzo fin de semana");
        inTransit.TenantId = tenantId;
        inTransit.RequestedAt = requestedAt.AddDays(-1);
        var shippedAt = requestedAt.AddDays(-1).AddHours(3);
        // The seeder runs unauthenticated, so tenant filters must be bypassed explicitly.
        var originLevels = await db.StockLevels.IgnoreQueryFilters()
            .Where(s => s.StoreId == inTransit.FromStoreId && s.Quantity > 0)
            .OrderByDescending(s => s.Quantity)
            .Take(2)
            .ToListAsync(ct);
        foreach (var level in originLevels)
        {
            var product = products.Single(p => p.Id == level.ProductId);
            var quantity = Math.Min(15, level.Quantity);
            inTransit.AddLine(product.Id, product.Sku, quantity);
            level.Decrease(quantity, product.Sku);
            db.StockMovements.Add(new StockMovement
            {
                TenantId = tenantId, StoreId = inTransit.FromStoreId, ProductId = product.Id, Quantity = -quantity,
                Type = MovementType.TransferOut, Reference = inTransit.Number, UserId = userId, CreatedAt = shippedAt
            });
        }
        inTransit.MarkShipped(shippedAt);
        db.Transfers.Add(inTransit);

        var requested = Transfer.Create($"T-{stores[0].Code}-SEED-001", stores[0].Id, stores[1].Id, userId, "Reposición semanal El Alto");
        requested.TenantId = tenantId;
        requested.RequestedAt = requestedAt;
        requested.AddLine(products[0].Id, products[0].Sku, 20);
        requested.AddLine(products[8].Id, products[8].Sku, 30);
        db.Transfers.Add(requested);

        var cancelled = Transfer.Create($"T-{stores[3].Code}-SEED-002", stores[3].Id, stores[2].Id, userId, "Pedido duplicado");
        cancelled.TenantId = tenantId;
        cancelled.RequestedAt = requestedAt.AddDays(-3);
        cancelled.AddLine(products[13].Id, products[13].Sku, 12);
        cancelled.Cancel();
        db.Transfers.Add(cancelled);
    }

    private async Task SeedAltiplanoMarketAsync(CancellationToken ct)
    {
        var tenant = new Tenant { Name = "Altiplano Market", Slug = "altiplano-market" };
        db.Tenants.Add(tenant);
        var store = new Store { TenantId = tenant.Id, Code = "ORU-01", Name = "Oruro Plaza", City = "Oruro" };
        db.Stores.Add(store);
        NewUser(tenant.Id, "admin@altiplano.demo", "Jorge Flores", UserRole.Admin, null);

        for (var i = 0; i < 6; i++)
        {
            var (sku, name, category, price, cost, _) = Catalog[i];
            var product = new Product
            {
                TenantId = tenant.Id, Sku = sku, Barcode = Ean13("777", 200001 + i), Name = name,
                Category = category, Price = price + 0.5m, Cost = cost
            };
            db.Products.Add(product);
            var level = StockLevel.Create(store.Id, product.Id, 50);
            level.TenantId = tenant.Id;
            db.StockLevels.Add(level);
        }
        await db.SaveChangesAsync(ct);
    }

    private User NewUser(Guid tenantId, string email, string name, UserRole role, Guid? storeId)
    {
        var user = new User { TenantId = tenantId, Email = email, FullName = name, Role = role, StoreId = storeId };
        user.PasswordHash = hasher.HashPassword(user, DemoPassword);
        db.Users.Add(user);
        return user;
    }

    /// <summary>Builds a valid EAN-13 (777 is Bolivia's GS1 prefix) so barcodes scan correctly in the mobile app.</summary>
    public static string Ean13(string prefix, int item)
    {
        var body = (prefix + item.ToString().PadLeft(9, '0'))[..12];
        var sum = body.Select((c, i) => (c - '0') * (i % 2 == 0 ? 1 : 3)).Sum();
        return body + (10 - sum % 10) % 10;
    }
}
