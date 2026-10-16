using RetailOps.Domain.Common;
using RetailOps.Domain.Entities;
using RetailOps.Domain.Enums;

namespace RetailOps.UnitTests.Domain;

public class TransferTests
{
    private static Transfer NewTransfer(bool withLine = true)
    {
        var transfer = Transfer.Create("T-1", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        if (withLine) transfer.AddLine(Guid.NewGuid(), "SKU-1", 5);
        return transfer;
    }

    [Fact]
    public void Happy_path_goes_requested_intransit_received()
    {
        var transfer = NewTransfer();
        var now = DateTimeOffset.UtcNow;

        transfer.MarkShipped(now);
        Assert.Equal(TransferStatus.InTransit, transfer.Status);

        transfer.MarkReceived(now.AddHours(3));
        Assert.Equal(TransferStatus.Received, transfer.Status);
        Assert.Equal(now.AddHours(3), transfer.ReceivedAt);
    }

    [Fact]
    public void Cannot_transfer_to_same_store()
    {
        var store = Guid.NewGuid();

        var ex = Assert.Throws<DomainException>(() => Transfer.Create("T-1", store, store, Guid.NewGuid()));
        Assert.Equal("transfer.same_store", ex.Code);
    }

    [Fact]
    public void Cannot_receive_before_shipping()
    {
        var transfer = NewTransfer();

        var ex = Assert.Throws<DomainException>(() => transfer.MarkReceived(DateTimeOffset.UtcNow));
        Assert.Equal("transfer.invalid_state", ex.Code);
    }

    [Fact]
    public void Cannot_cancel_once_in_transit()
    {
        var transfer = NewTransfer();
        transfer.MarkShipped(DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(transfer.Cancel);
        Assert.Equal(TransferStatus.InTransit, transfer.Status);
    }

    [Fact]
    public void Cannot_ship_an_empty_transfer()
    {
        var transfer = NewTransfer(withLine: false);

        var ex = Assert.Throws<DomainException>(() => transfer.MarkShipped(DateTimeOffset.UtcNow));
        Assert.Equal("transfer.empty", ex.Code);
    }

    [Fact]
    public void Lines_cannot_change_after_shipping()
    {
        var transfer = NewTransfer();
        transfer.MarkShipped(DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() => transfer.AddLine(Guid.NewGuid(), "SKU-2", 1));
    }
}
