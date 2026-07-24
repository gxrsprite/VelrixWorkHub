using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.Inventory;
public interface IInventoryTransactionRepository
{
    IReadOnlyList<InventoryTransaction> List();
    void Add(InventoryTransaction item);
}
