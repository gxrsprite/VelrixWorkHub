using VelrixWorkHub.Application.Reports;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class ErpSettlementTests
{
    [Fact]
    public void SupplierPayables_ExcludeCancelledAndSplitByStatus()
    {
        var supplier = Guid.CreateVersion7();
        var product = new Product("SKU-1", "标准包", "套", 10, null);
        var received = new PurchaseOrder("PO-1", supplier, product.Id, DateOnly.FromDateTime(DateTime.Today), 2, 100);
        received.SetStatus(PurchaseOrderStatus.Submitted);
        received.SetStatus(PurchaseOrderStatus.Received);
        var cancelled = new PurchaseOrder("PO-2", supplier, product.Id, DateOnly.FromDateTime(DateTime.Today), 1, 999);
        cancelled.SetStatus(PurchaseOrderStatus.Cancelled);

        var result = ErpSettlementService.SupplierPayables(new[] { received, cancelled }).Single();

        Assert.Equal(1, result.OrderCount);
        Assert.Equal(200m, result.TotalAmount);
        Assert.Equal(200m, result.CompletedAmount);
        Assert.Equal(0m, result.DraftAmount);
    }

    [Fact]
    public void CustomerReceivables_SplitDraftSubmittedAndShipped()
    {
        var customer = Guid.CreateVersion7();
        var product = new Product("SKU-1", "标准包", "套", 10, null);
        var draft = new SalesOrder("SO-1", customer, product.Id, DateOnly.FromDateTime(DateTime.Today), 1, 100);
        var submitted = new SalesOrder("SO-2", customer, product.Id, DateOnly.FromDateTime(DateTime.Today), 2, 100);
        submitted.SetStatus(SalesOrderStatus.Submitted);
        var shipped = new SalesOrder("SO-3", customer, product.Id, DateOnly.FromDateTime(DateTime.Today), 3, 100);
        shipped.SetStatus(SalesOrderStatus.Submitted);
        shipped.SetStatus(SalesOrderStatus.Shipped);

        var result = ErpSettlementService.CustomerReceivables(new[] { draft, submitted, shipped }).Single();

        Assert.Equal(6, result.TotalAmount / 100);
        Assert.Equal(100m, result.DraftAmount);
        Assert.Equal(200m, result.InProgressAmount);
        Assert.Equal(300m, result.CompletedAmount);
    }

    [Fact]
    public void SupplierPayables_SubtractActiveSettlementsAndIgnoreVoidedSettlements()
    {
        var supplier = Guid.CreateVersion7();
        var product = new Product("SKU-1", "标准包", "套", 10, null);
        var order = new PurchaseOrder("PO-SETTLED", supplier, product.Id, DateOnly.FromDateTime(DateTime.Today), 2, 100);
        var active = new ErpSettlement("PAY-1", order.Id, supplier, ErpSettlementKind.Payable, 80, DateOnly.FromDateTime(DateTime.Today));
        var voided = new ErpSettlement("PAY-2", order.Id, supplier, ErpSettlementKind.Payable, 20, DateOnly.FromDateTime(DateTime.Today));
        voided.Void("测试撤销");

        var result = ErpSettlementService.SupplierPayables(new[] { order }, new[] { active, voided }).Single();

        Assert.Equal(200m, result.TotalAmount);
        Assert.Equal(80m, result.SettledAmount);
        Assert.Equal(120m, result.OutstandingAmount);
    }

    [Fact]
    public void CustomerReceivables_SubtractActiveSettlementsFromOutstandingAmount()
    {
        var customer = Guid.CreateVersion7();
        var product = new Product("SKU-1", "标准包", "套", 10, null);
        var order = new SalesOrder("SO-SETTLED", customer, product.Id, DateOnly.FromDateTime(DateTime.Today), 3, 100);
        var settlement = new ErpSettlement("REC-1", order.Id, customer, ErpSettlementKind.Receivable, 150, DateOnly.FromDateTime(DateTime.Today));

        var result = ErpSettlementService.CustomerReceivables(new[] { order }, new[] { settlement }).Single();

        Assert.Equal(300m, result.TotalAmount);
        Assert.Equal(150m, result.SettledAmount);
        Assert.Equal(150m, result.OutstandingAmount);
    }
}
