using VelrixWorkHub.Application.Reports;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PurchaseOrderCloseTests
{
    [Fact]
    public void ReceivedOrderCanCloseAndReopen()
    {
        var order = new PurchaseOrder("PO-CLOSE-01", Guid.CreateVersion7(), Guid.CreateVersion7(), DateOnly.FromDateTime(DateTime.Today), 1, 10m);
        order.SetStatus(PurchaseOrderStatus.Submitted);
        order.SetStatus(PurchaseOrderStatus.Received);

        order.SetStatus(PurchaseOrderStatus.Closed);
        Assert.Equal(PurchaseOrderStatus.Closed, order.Status);

        order.SetStatus(PurchaseOrderStatus.Received);
        Assert.Equal(PurchaseOrderStatus.Received, order.Status);
    }

    [Fact]
    public void DraftAndSubmittedOrdersCannotClose()
    {
        var order = new PurchaseOrder("PO-CLOSE-02", Guid.CreateVersion7(), Guid.CreateVersion7(), DateOnly.FromDateTime(DateTime.Today), 1, 10m);

        Assert.Throws<InvalidOperationException>(() => order.SetStatus(PurchaseOrderStatus.Closed));
        order.SetStatus(PurchaseOrderStatus.Submitted);
        Assert.Throws<InvalidOperationException>(() => order.SetStatus(PurchaseOrderStatus.Closed));
    }

    [Fact]
    public void ClosedOrderRemainsCompletedInSettlementAndReconciliation()
    {
        var product = new Product("SKU-CLOSE", "标准包", "套", 10, null);
        var order = new PurchaseOrder("PO-CLOSE-03", Guid.CreateVersion7(), product.Id, DateOnly.FromDateTime(DateTime.Today), 2, 100m);
        order.SetStatus(PurchaseOrderStatus.Submitted);
        order.SetStatus(PurchaseOrderStatus.Received);
        order.SetStatus(PurchaseOrderStatus.Closed);
        var warehouse = Guid.CreateVersion7();
        var transactions = new[] { new InventoryTransaction(product.Id, warehouse, InventoryTransactionKind.Inbound, 2, "PO-CLOSE-03-IN", order.OrderDate, null) };

        var balance = ErpSettlementService.SupplierPayables(new[] { order }).Single();
        var reconciliation = ErpReconciliationService.Purchase(new[] { order }, transactions).Single();

        Assert.Equal(order.Amount, balance.CompletedAmount);
        Assert.Equal(ErpReconciliationStatus.Matched, reconciliation.Status);
    }
}
