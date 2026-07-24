using VelrixWorkHub.Application.Reports;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmpProjects;

public sealed record PmpProjectCommercialInsight(
    bool HasProjectScopedOrders,
    int SalesOrderCount,
    int ShippedOrderCount,
    int PendingShipmentOrderCount,
    decimal SalesOrderAmount,
    decimal ShippedOrderAmount,
    decimal PendingShipmentAmount,
    decimal SalesOrderQuantity,
    decimal ShippedOrderQuantity,
    decimal PendingShipmentQuantity,
    decimal ReceivedAmount,
    decimal ReceivableAmount,
    decimal ActiveContractAmount,
    decimal ContractedOrderAmount,
    decimal UnorderedContractAmount);

public sealed record PmpProjectFulfillmentOrder(
    SalesOrder Order,
    decimal ReceivedAmount)
{
    public decimal ReceivableAmount => decimal.Round(Order.Amount - ReceivedAmount, 2);
}

public static class PmpProjectCommercialInsightService
{
    public static PmpProjectCommercialInsight Build(
        PmpProject project,
        IEnumerable<SalesOrder> salesOrders,
        IEnumerable<ErpSettlement> settlements,
        IEnumerable<SalesContract> contracts)
    {
        var customerOrders = CustomerOrders(project, salesOrders);
        var hasAnyProjectAssignments = customerOrders.Any(x => x.PmpProjectId is not null && x.Status != SalesOrderStatus.Cancelled);
        var hasProjectScopedOrders = project.CustomerId is not null && customerOrders.Any(x => x.PmpProjectId == project.Id && x.Status != SalesOrderStatus.Cancelled);
        var scopedOrders = ScopeOrders(project, customerOrders, hasAnyProjectAssignments);
        var activeOrders = scopedOrders.Where(x => x.Status != SalesOrderStatus.Cancelled).ToArray();
        var shippedOrders = activeOrders.Where(x => x.Status == SalesOrderStatus.Shipped).ToArray();
        var scopedOrderIds = scopedOrders.Select(x => x.Id).ToHashSet();
        var scopedSettlements = settlements.Where(x => scopedOrderIds.Contains(x.OrderId));
        var customerInsight = project.CustomerId is Guid insightCustomerId
            ? CustomerErpInsightService.Build(insightCustomerId, scopedOrders, scopedSettlements)
            : null;
        var contractInsight = project.CustomerId is Guid contractCustomerId
            ? CustomerContractInsightService.Build(contractCustomerId, contracts, scopedOrders)
            : null;

        return new PmpProjectCommercialInsight(
            hasProjectScopedOrders,
            customerInsight?.OrderCount ?? 0,
            customerInsight?.ShippedOrderCount ?? 0,
            activeOrders.Count(x => x.Status != SalesOrderStatus.Shipped),
            customerInsight?.SalesAmount ?? 0,
            shippedOrders.Sum(x => x.Amount),
            activeOrders.Where(x => x.Status != SalesOrderStatus.Shipped).Sum(x => x.Amount),
            activeOrders.Sum(x => x.Quantity),
            shippedOrders.Sum(x => x.Quantity),
            activeOrders.Where(x => x.Status != SalesOrderStatus.Shipped).Sum(x => x.Quantity),
            customerInsight?.ReceivedAmount ?? 0,
            customerInsight?.ReceivableAmount ?? 0,
            contractInsight?.ActiveContractAmount ?? 0,
            contractInsight?.ContractedOrderAmount ?? 0,
            contractInsight?.UnorderedContractAmount ?? 0);
    }

    public static PmpProjectFulfillmentOrder[] Orders(PmpProject project, IEnumerable<SalesOrder> salesOrders, IEnumerable<ErpSettlement> settlements)
    {
        var customerOrders = CustomerOrders(project, salesOrders);
        var hasAnyProjectAssignments = customerOrders.Any(x => x.PmpProjectId is not null && x.Status != SalesOrderStatus.Cancelled);
        var scopedOrders = ScopeOrders(project, customerOrders, hasAnyProjectAssignments)
            .Where(x => x.Status != SalesOrderStatus.Cancelled)
            .OrderByDescending(x => x.OrderDate)
            .ThenByDescending(x => x.OrderNo)
            .ToArray();
        var receivedByOrder = settlements
            .Where(x => x.Kind == ErpSettlementKind.Receivable && x.Status == ErpSettlementStatus.Active)
            .GroupBy(x => x.OrderId)
            .ToDictionary(x => x.Key, x => decimal.Round(x.Sum(item => item.Amount), 2));
        return scopedOrders.Select(order => new PmpProjectFulfillmentOrder(order, receivedByOrder.GetValueOrDefault(order.Id))).ToArray();
    }

    private static SalesOrder[] CustomerOrders(PmpProject project, IEnumerable<SalesOrder> salesOrders) => project.CustomerId is Guid customerId
        ? salesOrders.Where(x => x.CustomerId == customerId).ToArray()
        : [];

    private static SalesOrder[] ScopeOrders(PmpProject project, IReadOnlyList<SalesOrder> customerOrders, bool hasAnyProjectAssignments) => hasAnyProjectAssignments
        ? customerOrders.Where(x => x.PmpProjectId == project.Id).ToArray()
        : customerOrders.ToArray();
}
