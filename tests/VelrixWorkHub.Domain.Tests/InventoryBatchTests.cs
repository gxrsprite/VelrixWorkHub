using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class InventoryBatchTests
{
    [Fact]
    public void TransactionPreservesBatchAndExpiryMetadata()
    {
        var occurredOn = new DateOnly(2026, 7, 15);
        var item = new InventoryTransaction(Guid.CreateVersion7(), Guid.CreateVersion7(), InventoryTransactionKind.Inbound, 5m, "INV-BATCH-01", occurredOn, null, batchNo: "LOT-20260715-001", expiryDate: new DateOnly(2027, 7, 15));

        Assert.Equal("LOT-20260715-001", item.BatchNo);
        Assert.Equal(new DateOnly(2027, 7, 15), item.ExpiryDate);
    }

    [Fact]
    public void ExpiryDateCannotBeEarlierThanTransactionDate()
    {
        Assert.Throws<ArgumentException>(() => new InventoryTransaction(Guid.CreateVersion7(), Guid.CreateVersion7(), InventoryTransactionKind.Inbound, 1m, "INV-BATCH-02", new DateOnly(2026, 7, 15), null, expiryDate: new DateOnly(2026, 7, 14)));
    }
}
