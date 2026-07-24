using VelrixWorkHub.Application.Reports;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class ErpReconciliationTests
{
    [Fact]
    public void Purchase_ReceivedOrderWithInboundIsMatched()
    {
        var product = new Product("SKU-1", "标准包", "套", 10, null);
        var supplier = Guid.CreateVersion7();
        var order = new PurchaseOrder("PO-1", supplier, product.Id, DateOnly.FromDateTime(DateTime.Today), 5, 10);
        order.SetStatus(PurchaseOrderStatus.Submitted);
        order.SetStatus(PurchaseOrderStatus.Received);
        var warehouse = Guid.CreateVersion7();
        var transactions = new[] { new InventoryTransaction(product.Id, warehouse, InventoryTransactionKind.Inbound, 5, "PO-1-IN", order.OrderDate, null) };

        var result = ErpReconciliationService.Purchase(new[] { order }, transactions).Single();

        Assert.Equal(ErpReconciliationStatus.Matched, result.Status);
        Assert.Equal(0, result.Difference);
    }

    [Fact]
    public void Purchase_ReceivedOrderWithoutInboundIsPending()
    {
        var product = new Product("SKU-1", "标准包", "套", 10, null);
        var order = new PurchaseOrder("PO-2", Guid.CreateVersion7(), product.Id, DateOnly.FromDateTime(DateTime.Today), 5, 10);
        order.SetStatus(PurchaseOrderStatus.Submitted);
        order.SetStatus(PurchaseOrderStatus.Received);

        var result = ErpReconciliationService.Purchase(new[] { order }, []).Single();

        Assert.Equal(ErpReconciliationStatus.Pending, result.Status);
        Assert.Equal(5, result.Difference);
    }

    [Fact]
    public void Sales_ShippedOrderWithOutboundIsMatched()
    {
        var product = new Product("SKU-1", "标准包", "套", 10, null);
        var customer = Guid.CreateVersion7();
        var order = new SalesOrder("SO-1", customer, product.Id, DateOnly.FromDateTime(DateTime.Today), 3, 20);
        order.SetStatus(SalesOrderStatus.Submitted);
        order.SetStatus(SalesOrderStatus.Shipped);
        var warehouse = Guid.CreateVersion7();
        var transactions = new[] { new InventoryTransaction(product.Id, warehouse, InventoryTransactionKind.Outbound, 3, "SO-1-OUT", order.OrderDate, null) };

        var result = ErpReconciliationService.Sales(new[] { order }, transactions).Single();

        Assert.Equal(ErpReconciliationStatus.Matched, result.Status);
    }

    [Fact]
    public void CancelledOrderIsNotApplicable()
    {
        var product = new Product("SKU-1", "标准包", "套", 10, null);
        var order = new SalesOrder("SO-2", Guid.CreateVersion7(), product.Id, DateOnly.FromDateTime(DateTime.Today), 3, 20);
        order.SetStatus(SalesOrderStatus.Cancelled);

        var result = ErpReconciliationService.Sales(new[] { order }, []).Single();

        Assert.Equal(ErpReconciliationStatus.NotApplicable, result.Status);
    }
}
