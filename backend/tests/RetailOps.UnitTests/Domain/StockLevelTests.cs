using RetailOps.Domain.Common;
using RetailOps.Domain.Entities;

namespace RetailOps.UnitTests.Domain;

public class StockLevelTests
{
    [Fact]
    public void Decrease_reduces_quantity()
    {
        var level = StockLevel.Create(Guid.NewGuid(), Guid.NewGuid(), initialQuantity: 10);

        level.Decrease(4, "SKU-1");

        Assert.Equal(6, level.Quantity);
    }

    [Fact]
    public void Decrease_beyond_available_throws_and_keeps_quantity()
    {
        var level = StockLevel.Create(Guid.NewGuid(), Guid.NewGuid(), initialQuantity: 3);

        var ex = Assert.Throws<InsufficientStockException>(() => level.Decrease(5, "SKU-1"));

        Assert.Equal(3, ex.Available);
        Assert.Equal(5, ex.Requested);
        Assert.Equal(3, level.Quantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void Non_positive_movements_are_rejected(int quantity)
    {
        var level = StockLevel.Create(Guid.NewGuid(), Guid.NewGuid(), initialQuantity: 5);

        Assert.Throws<DomainException>(() => level.Increase(quantity));
        Assert.Throws<DomainException>(() => level.Decrease(quantity, "SKU-1"));
    }

    [Theory]
    [InlineData(10, 10, true)]
    [InlineData(11, 10, false)]
    [InlineData(0, 10, true)]
    public void IsLow_when_at_or_below_reorder_point(int quantity, int reorderPoint, bool expected)
    {
        var level = StockLevel.Create(Guid.NewGuid(), Guid.NewGuid(), quantity, reorderPoint);

        Assert.Equal(expected, level.IsLow);
    }
}
