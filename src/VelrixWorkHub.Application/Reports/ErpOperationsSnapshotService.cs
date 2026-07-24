using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Reports;

public sealed record ErpInventoryRisk(
    Guid ProductId,
    decimal OnHandQuantity,
    decimal SubmittedSalesQuantity)
{
    public decimal FrozenQuantity => decimal.Round(SubmittedSalesQuantity, 2);
    public decimal AvailableQuantity => decimal.Round(OnHandQuantity - FrozenQuantity, 2);
}

public sealed record ErpOperationsSnapshot(
    int PendingPurchaseOrderCount,
    decimal PendingPurchaseAmount,
    int PendingSalesOrderCount,
    decimal PendingSalesAmount,
    decimal PayableAmount,
    decimal ReceivableAmount,
    decimal InventoryQuantity,
    IReadOnlyList<ErpInventoryRisk> InventoryRisks);

public static class ErpOperationsSnapshotService
{
    public static ErpOperationsSnapshot Build(
        IEnumerable<PurchaseOrder> purchaseOrders,
        IEnumerable<SalesOrder> salesOrders,
        IEnumerable<InventoryBalance> inventoryBalances,
        IEnumerable<ErpSettlement> settlements)
    {
        var purchases = purchaseOrders.Where(x => x.Status != PurchaseOrderStatus.Cancelled).ToArray();
        var sales = salesOrders.Where(x => x.Status != SalesOrderStatus.Cancelled).ToArray();
        var activeSettlements = settlements.Where(x => x.Status == ErpSettlementStatus.Active).ToArray();

        var payable = RemainingAmount(purchases.Select(x => (x.Id, x.Amount)), activeSettlements, ErpSettlementKind.Payable);
        var receivable = RemainingAmount(sales.Select(x => (x.Id, x.Amount)), activeSettlements, ErpSettlementKind.Receivable);
        var submittedSalesByProduct = sales
            .Where(x => x.Status == SalesOrderStatus.Submitted)
            .GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity));
        var onHandByProduct = inventoryBalances
            .GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity));
        var risks = submittedSalesByProduct
            .Select(x => new ErpInventoryRisk(x.Key, onHandByProduct.GetValueOrDefault(x.Key), x.Value))
            .Where(x => x.AvailableQuantity < 0)
            .OrderBy(x => x.AvailableQuantity)
            .ThenBy(x => x.ProductId)
            .ToArray();

        return new ErpOperationsSnapshot(
            purchases.Count(x => x.Status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Submitted),
            purchases.Where(x => x.Status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Submitted).Sum(x => x.Amount),
            sales.Count(x => x.Status is SalesOrderStatus.Draft or SalesOrderStatus.Submitted),
            sales.Where(x => x.Status is SalesOrderStatus.Draft or SalesOrderStatus.Submitted).Sum(x => x.Amount),
            payable,
            receivable,
            inventoryBalances.Sum(x => x.Quantity),
            risks);
    }

    private static decimal RemainingAmount(
        IEnumerable<(Guid Id, decimal Amount)> orders,
        IEnumerable<ErpSettlement> settlements,
        ErpSettlementKind kind)
    {
        var settledByOrder = settlements
            .Where(x => x.Kind == kind)
            .GroupBy(x => x.OrderId)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));
        return decimal.Round(orders.Sum(x => Math.Max(x.Amount - settledByOrder.GetValueOrDefault(x.Id), 0)), 2);
    }
}
