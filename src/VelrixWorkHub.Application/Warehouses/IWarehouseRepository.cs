using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.Warehouses;
public interface IWarehouseRepository
{
    IReadOnlyList<Warehouse> List();
    void Add(Warehouse item);
    void Update(Warehouse item);
    void Remove(Guid id);
    void AddLocation(WarehouseLocation item);
    void RemoveLocation(Guid id);
    void UpsertLocationProductCapacity(WarehouseLocationProductCapacity item);
    void RemoveLocationProductCapacity(Guid locationId, Guid productId);
}
