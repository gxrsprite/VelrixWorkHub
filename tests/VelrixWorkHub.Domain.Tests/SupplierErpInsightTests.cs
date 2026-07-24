using VelrixWorkHub.Application.Reports;

namespace VelrixWorkHub.Domain.Tests;

public sealed class SupplierErpInsightTests
{
    [Fact]
    public void Build_AggregatesValidPurchasesAndActivePaymentsForSupplier()
    {
        var supplier = Guid.NewGuid(); var product = Guid.NewGuid();
        var received = PurchaseOrder.Restore(Guid.NewGuid(), "PO-SUP-01", supplier, product, DateOnly.FromDateTime(DateTime.Today), 2, 100m, PurchaseOrderStatus.Received);
        var cancelled = PurchaseOrder.Restore(Guid.NewGuid(), "PO-SUP-02", supplier, product, DateOnly.FromDateTime(DateTime.Today), 1, 50m, PurchaseOrderStatus.Cancelled);
        var other = new PurchaseOrder("PO-SUP-03", Guid.NewGuid(), product, DateOnly.FromDateTime(DateTime.Today), 1, 300m);
        var paid = new ErpSettlement("PAY-SUP-01", received.Id, supplier, ErpSettlementKind.Payable, 60m, DateOnly.FromDateTime(DateTime.Today));
        var voided = new ErpSettlement("PAY-SUP-02", received.Id, supplier, ErpSettlementKind.Payable, 40m, DateOnly.FromDateTime(DateTime.Today));
        voided.Void("付款回单错误");

        var result = SupplierErpInsightService.Build(supplier, [received, cancelled, other], [paid, voided]);

        Assert.Equal(1, result.OrderCount); Assert.Equal(1, result.ReceivedOrderCount); Assert.Equal(200m, result.PurchaseAmount); Assert.Equal(60m, result.PaidAmount); Assert.Equal(140m, result.PayableAmount);
    }
}
