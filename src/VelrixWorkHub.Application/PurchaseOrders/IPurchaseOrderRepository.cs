using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.PurchaseOrders;
public interface IPurchaseOrderRepository
{
    IReadOnlyList<PurchaseOrder> List();
    void Add(PurchaseOrder item);
    void Update(PurchaseOrder item);
}
