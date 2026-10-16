using RetailOps.Domain.Common;
using RetailOps.Domain.Entities;

namespace RetailOps.UnitTests.Domain;

public class SaleTests
{
    private static Product NewProduct(decimal price, bool active = true) => new()
    {
        Sku = "SKU-" + price, Barcode = "7770000000001", Name = "Product " + price, Category = "Test", Price = price, IsActive = active
    };

    [Fact]
    public void Total_is_sum_of_lines()
    {
        var sale = new Sale { Number = "V-1" };

        sale.AddLine(NewProduct(7.50m), 2);
        sale.AddLine(NewProduct(12.00m), 1);

        Assert.Equal(27.00m, sale.Total);
        Assert.Equal(2, sale.Lines.Count);
    }

    [Fact]
    public void Adding_same_product_twice_merges_lines()
    {
        var sale = new Sale { Number = "V-1" };
        var milk = NewProduct(7.50m);

        sale.AddLine(milk, 1);
        sale.AddLine(milk, 3);

        var line = Assert.Single(sale.Lines);
        Assert.Equal(4, line.Quantity);
        Assert.Equal(30.00m, sale.Total);
    }

    [Fact]
    public void Price_is_captured_at_sale_time()
    {
        var sale = new Sale { Number = "V-1" };
        var product = NewProduct(10m);

        sale.AddLine(product, 1);
        product.Price = 99m;

        Assert.Equal(10m, sale.Lines[0].UnitPrice);
    }

    [Fact]
    public void Inactive_products_cannot_be_sold()
    {
        var sale = new Sale { Number = "V-1" };

        var ex = Assert.Throws<DomainException>(() => sale.AddLine(NewProduct(5m, active: false), 1));
        Assert.Equal("sale.inactive_product", ex.Code);
    }

    [Fact]
    public void Empty_sale_is_rejected()
    {
        var sale = new Sale { Number = "V-1" };

        Assert.Throws<DomainException>(sale.EnsureNotEmpty);
    }
}
