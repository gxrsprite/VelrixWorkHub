using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PurchaseOrderInventoryServiceTests
{
    [Fact]
    public void Get_SeparatesExpectedInboundFromReceivedAndCancelledOrders()
    {
        var supplier = Guid.CreateVersion7();
        var product = Guid.CreateVersion7();
        var draft = new PurchaseOrder("PO-DRAFT", supplier, product, Today, 4, 10);
        var submitted = new PurchaseOrder("PO-SUBMITTED", supplier, product, Today, 3, 10);
        submitted.SetStatus(PurchaseOrderStatus.Submitted);
        var received = new PurchaseOrder("PO-RECEIVED", supplier, product, Today, 2, 10);
        received.SetStatus(PurchaseOrderStatus.Submitted);
        received.SetStatus(PurchaseOrderStatus.Received);
        var cancelled = new PurchaseOrder("PO-CANCELLED", supplier, product, Today, 5, 10);
        cancelled.SetStatus(PurchaseOrderStatus.Cancelled);

        var result = PurchaseOrderInventoryService.Get(product, new[] { new InventoryBalance(product, Guid.CreateVersion7(), 10) }, new[] { draft, submitted, received, cancelled });

        Assert.Equal(10m, result.OnHandQuantity);
        Assert.Equal(3m, result.SubmittedQuantity);
        Assert.Equal(4m, result.DraftQuantity);
        Assert.Equal(13m, result.ProjectedQuantity);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
}
