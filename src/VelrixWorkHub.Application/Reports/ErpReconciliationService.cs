using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Reports;

public enum ErpReconciliationStatus { Matched, Pending, NotApplicable }

public sealed record ErpReconciliationItem(
    Guid OrderId,
    string OrderNo,
    decimal OrderQuantity,
    decimal TransactionQuantity,
    ErpReconciliationStatus Status,
    string ReferenceNo)
{
    public decimal Difference => decimal.Round(OrderQuantity - TransactionQuantity, 2);
}

public static class ErpReconciliationService
{
    public static IReadOnlyList<ErpReconciliationItem> Purchase(IEnumerable<PurchaseOrder> orders, IEnumerable<InventoryTransaction> transactions)
        => orders.Select(order => Match(order.Id, order.OrderNo, order.Quantity, order.Status is PurchaseOrderStatus.Received or PurchaseOrderStatus.Closed, $"{order.OrderNo}-IN", order.ProductId, transactions)).ToArray();

    public static IReadOnlyList<ErpReconciliationItem> Sales(IEnumerable<SalesOrder> orders, IEnumerable<InventoryTransaction> transactions)
        => orders.Select(order => Match(order.Id, order.OrderNo, order.Quantity, order.Status == SalesOrderStatus.Shipped, $"{order.OrderNo}-OUT", order.ProductId, transactions)).ToArray();

    private static ErpReconciliationItem Match(Guid orderId, string orderNo, decimal quantity, bool completed, string referenceNo, Guid productId, IEnumerable<InventoryTransaction> transactions)
    {
        if (!completed)
        {
            return new(orderId, orderNo, quantity, 0, ErpReconciliationStatus.NotApplicable, referenceNo);
        }

        var transactionQuantity = transactions
            .Where(x => x.SourceNo.Equals(referenceNo, StringComparison.OrdinalIgnoreCase) && x.ProductId == productId)
            .Sum(x => x.Quantity);
        var status = transactionQuantity == quantity ? ErpReconciliationStatus.Matched : ErpReconciliationStatus.Pending;
        return new(orderId, orderNo, quantity, transactionQuantity, status, referenceNo);
    }
}
