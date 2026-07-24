using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Reports;

public sealed record ErpSettlementBalance(Guid PartyId, int OrderCount, decimal TotalAmount, decimal DraftAmount, decimal InProgressAmount, decimal CompletedAmount, decimal SettledAmount)
{
    public decimal OutstandingAmount => decimal.Round(Math.Max(TotalAmount - SettledAmount, 0), 2);
}

public static class ErpSettlementService
{
    public static IReadOnlyList<ErpSettlementBalance> SupplierPayables(IEnumerable<PurchaseOrder> orders, IEnumerable<ErpSettlement>? settlements = null)
        => orders.Where(x => x.Status != PurchaseOrderStatus.Cancelled)
            .GroupBy(x => x.SupplierId)
            .Select(x => BuildBalance(x.Key, x, ErpSettlementKind.Payable, settlements))
            .OrderByDescending(x => x.TotalAmount)
            .ToArray();

    public static IReadOnlyList<ErpSettlementBalance> CustomerReceivables(IEnumerable<SalesOrder> orders, IEnumerable<ErpSettlement>? settlements = null)
        => orders.Where(x => x.Status != SalesOrderStatus.Cancelled)
            .GroupBy(x => x.CustomerId)
            .Select(x => BuildBalance(x.Key, x, ErpSettlementKind.Receivable, settlements))
            .OrderByDescending(x => x.TotalAmount)
            .ToArray();

    private static ErpSettlementBalance BuildBalance<TOrder>(Guid partyId, IEnumerable<TOrder> orders, ErpSettlementKind kind, IEnumerable<ErpSettlement>? settlements)
        where TOrder : class
    {
        var orderArray = orders.ToArray();
        var totalAmount = orderArray.Sum(GetAmount);
        var settledAmount = settlements is null
            ? 0
            : settlements.Where(x => x.Kind == kind && x.Status == ErpSettlementStatus.Active && orderArray.Any(order => GetId(order) == x.OrderId)).Sum(x => x.Amount);
        return kind == ErpSettlementKind.Payable
            ? new(partyId, orderArray.Length, totalAmount, orderArray.OfType<PurchaseOrder>().Where(x => x.Status == PurchaseOrderStatus.Draft).Sum(x => x.Amount), orderArray.OfType<PurchaseOrder>().Where(x => x.Status == PurchaseOrderStatus.Submitted).Sum(x => x.Amount), orderArray.OfType<PurchaseOrder>().Where(x => x.Status is PurchaseOrderStatus.Received or PurchaseOrderStatus.Closed).Sum(x => x.Amount), settledAmount)
            : new(partyId, orderArray.Length, totalAmount, orderArray.OfType<SalesOrder>().Where(x => x.Status == SalesOrderStatus.Draft).Sum(x => x.Amount), orderArray.OfType<SalesOrder>().Where(x => x.Status == SalesOrderStatus.Submitted).Sum(x => x.Amount), orderArray.OfType<SalesOrder>().Where(x => x.Status == SalesOrderStatus.Shipped).Sum(x => x.Amount), settledAmount);
    }

    private static Guid GetId<TOrder>(TOrder order) => order switch
    {
        PurchaseOrder purchase => purchase.Id,
        SalesOrder sales => sales.Id,
        _ => throw new ArgumentException("不支持的订单类型。", nameof(order))
    };

    private static decimal GetAmount<TOrder>(TOrder order) => order switch
    {
        PurchaseOrder purchase => purchase.Amount,
        SalesOrder sales => sales.Amount,
        _ => throw new ArgumentException("不支持的订单类型。", nameof(order))
    };
}
