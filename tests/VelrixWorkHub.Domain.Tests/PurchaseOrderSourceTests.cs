using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PurchaseOrderSourceTests
{
    [Fact]
    public void NonManualOrderRequiresAndPreservesSourceDocument()
    {
        var supplier = Guid.CreateVersion7();
        var product = Guid.CreateVersion7();

        Assert.Throws<ArgumentException>(() => new PurchaseOrder("PO-SOURCE-01", supplier, product, DateOnly.FromDateTime(DateTime.Today), 1, 10m, PurchaseOrderSourceKind.Contract));

        var order = new PurchaseOrder("PO-SOURCE-02", supplier, product, DateOnly.FromDateTime(DateTime.Today), 1, 10m, PurchaseOrderSourceKind.Contract, "SC-2026-001");

        Assert.Equal(PurchaseOrderSourceKind.Contract, order.SourceKind);
        Assert.Equal("SC-2026-001", order.SourceDocumentNo);
    }
}
