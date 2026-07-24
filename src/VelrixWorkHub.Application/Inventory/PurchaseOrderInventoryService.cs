using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Inventory;

public sealed record PurchaseOrderInventoryAvailability(decimal OnHandQuantity, decimal SubmittedQuantity, decimal DraftQuantity)
{
    public decimal ProjectedQuantity => decimal.Round(OnHandQuantity + SubmittedQuantity, 2);
}

public static class PurchaseOrderInventoryService
{
    public static PurchaseOrderInventoryAvailability Get(Guid productId, IEnumerable<InventoryBalance> balances, IEnumerable<PurchaseOrder> orders)
    {
        var onHand = balances.Where(x => x.ProductId == productId).Sum(x => x.Quantity);
        var productOrders = orders.Where(x => x.ProductId == productId);
        return new PurchaseOrderInventoryAvailability(onHand, productOrders.Where(x => x.Status == PurchaseOrderStatus.Submitted).Sum(x => x.Quantity), productOrders.Where(x => x.Status == PurchaseOrderStatus.Draft).Sum(x => x.Quantity));
    }
}
