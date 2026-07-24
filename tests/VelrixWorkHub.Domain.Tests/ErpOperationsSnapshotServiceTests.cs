using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Application.Reports;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class ErpOperationsSnapshotServiceTests
{
    [Fact]
    public void Build_CombinesPendingOrdersBalancesAndInventoryRisks()
    {
        var supplier = Guid.NewGuid();
        var customer = Guid.NewGuid();
        var product = Guid.NewGuid();
        var warehouse = Guid.NewGuid();
        var purchase = PurchaseOrder.Restore(Guid.NewGuid(), "PO-OPS-001", supplier, product, new DateOnly(2026, 7, 13), 1, 100m, PurchaseOrderStatus.Submitted);
        var cancelledPurchase = PurchaseOrder.Restore(Guid.NewGuid(), "PO-OPS-CANCEL", supplier, product, new DateOnly(2026, 7, 13), 1, 500m, PurchaseOrderStatus.Cancelled);
        var submittedSale = SalesOrder.Restore(Guid.NewGuid(), "SO-OPS-001", customer, product, new DateOnly(2026, 7, 13), 5, 60m, SalesOrderStatus.Submitted);
        var draftSale = SalesOrder.Restore(Guid.NewGuid(), "SO-OPS-002", customer, product, new DateOnly(2026, 7, 13), 1, 50m, SalesOrderStatus.Draft);
        var cancelledSale = SalesOrder.Restore(Guid.NewGuid(), "SO-OPS-CANCEL", customer, product, new DateOnly(2026, 7, 13), 1, 900m, SalesOrderStatus.Cancelled);
        var payable = new ErpSettlement("PAY-OPS-001", purchase.Id, supplier, ErpSettlementKind.Payable, 20m, new DateOnly(2026, 7, 13));
        var receivable = new ErpSettlement("REC-OPS-001", submittedSale.Id, customer, ErpSettlementKind.Receivable, 100m, new DateOnly(2026, 7, 13));
        var voided = new ErpSettlement("REC-OPS-VOID", submittedSale.Id, customer, ErpSettlementKind.Receivable, 50m, new DateOnly(2026, 7, 13));
        voided.Void("录入错误");

        var result = ErpOperationsSnapshotService.Build(
            [purchase, cancelledPurchase],
            [submittedSale, draftSale, cancelledSale],
            [new InventoryBalance(product, warehouse, 2m)],
            [payable, receivable, voided]);

        Assert.Equal(1, result.PendingPurchaseOrderCount);
        Assert.Equal(100m, result.PendingPurchaseAmount);
        Assert.Equal(2, result.PendingSalesOrderCount);
        Assert.Equal(350m, result.PendingSalesAmount);
        Assert.Equal(80m, result.PayableAmount);
        Assert.Equal(250m, result.ReceivableAmount);
        Assert.Equal(2m, result.InventoryQuantity);
        var risk = Assert.Single(result.InventoryRisks);
        Assert.Equal(product, risk.ProductId);
        Assert.Equal(-3m, risk.AvailableQuantity);
    }
}
