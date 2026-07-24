using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Reports;

public sealed record CustomerErpInsight(Guid CustomerId, int OrderCount, int ShippedOrderCount, decimal SalesAmount, decimal ReceivedAmount)
{
    public decimal ReceivableAmount => decimal.Round(SalesAmount - ReceivedAmount, 2);
}

public static class CustomerErpInsightService
{
    public static CustomerErpInsight Build(Guid customerId, IEnumerable<SalesOrder> orders, IEnumerable<ErpSettlement> settlements)
    {
        var customerOrders = orders.Where(x => x.CustomerId == customerId && x.Status != SalesOrderStatus.Cancelled).ToArray();
        var received = settlements.Where(x => x.Kind == ErpSettlementKind.Receivable && x.Status == ErpSettlementStatus.Active && x.PartyId == customerId).Sum(x => x.Amount);
        return new CustomerErpInsight(customerId, customerOrders.Length, customerOrders.Count(x => x.Status == SalesOrderStatus.Shipped), customerOrders.Sum(x => x.Amount), received);
    }
}
