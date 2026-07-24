using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Inventory;

public sealed record SalesOrderInventoryAvailability(decimal OnHandQuantity, decimal SubmittedQuantity, decimal DraftQuantity)
{
    public decimal FrozenQuantity => decimal.Round(SubmittedQuantity, 2);
    public decimal AvailableQuantity => decimal.Round(OnHandQuantity - FrozenQuantity, 2);
}

public static class SalesOrderInventoryService
{
    public static SalesOrderInventoryAvailability Get(Guid productId, IEnumerable<InventoryBalance> balances, IEnumerable<SalesOrder> orders)
    {
        var onHand = balances.Where(x => x.ProductId == productId).Sum(x => x.Quantity);
        var productOrders = orders.Where(x => x.ProductId == productId);
        return new SalesOrderInventoryAvailability(onHand, productOrders.Where(x => x.Status == SalesOrderStatus.Submitted).Sum(x => x.Quantity), productOrders.Where(x => x.Status == SalesOrderStatus.Draft).Sum(x => x.Quantity));
    }
}
