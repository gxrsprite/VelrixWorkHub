using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Reports;

public sealed record SupplierErpInsight(Guid SupplierId, int OrderCount, int ReceivedOrderCount, decimal PurchaseAmount, decimal PaidAmount)
{
    public decimal PayableAmount => decimal.Round(PurchaseAmount - PaidAmount, 2);
}

public static class SupplierErpInsightService
{
    public static SupplierErpInsight Build(Guid supplierId, IEnumerable<PurchaseOrder> orders, IEnumerable<ErpSettlement> settlements)
    {
        var supplierOrders = orders.Where(x => x.SupplierId == supplierId && x.Status != PurchaseOrderStatus.Cancelled).ToArray();
        var paid = settlements.Where(x => x.Kind == ErpSettlementKind.Payable && x.Status == ErpSettlementStatus.Active && x.PartyId == supplierId).Sum(x => x.Amount);
        return new SupplierErpInsight(supplierId, supplierOrders.Length, supplierOrders.Count(x => x.Status is PurchaseOrderStatus.Received or PurchaseOrderStatus.Closed), supplierOrders.Sum(x => x.Amount), paid);
    }
}
