using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Application.MasterData;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.Warehouses;
public sealed class WarehouseService(IWarehouseRepository repository, IInventoryTransactionRepository? inventoryRepository = null)
{
    public IReadOnlyList<Warehouse> List(string? keyword = null, WarehouseStatus? status = null)
    {
        var query = repository.List().AsEnumerable(); var text = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(text)) query = query.Where(x => x.Code.Contains(text, StringComparison.OrdinalIgnoreCase) || x.Name.Contains(text, StringComparison.OrdinalIgnoreCase));
        if (status is not null) query = query.Where(x => x.Status == status);
        return query.ToArray();
    }
    public Warehouse Create(string code, string name, string? address, string? otherInfo = null) { var item = new Warehouse(code, name, address, otherInfo); EnsureUnique(item); repository.Add(item); return item; }
    public void Edit(Warehouse item, string code, string name, string? address, string? otherInfo = null) { item.Edit(code, name, address, otherInfo); EnsureUnique(item); repository.Update(item); }
    public void SetActive(Warehouse item, bool active) { item.SetActive(active); repository.Update(item); }
    public WarehouseLocation AddLocation(Warehouse warehouse, string code, string name) { var item = warehouse.AddLocation(code, name); EnsureLocationUnique(warehouse, item); repository.AddLocation(item); return item; }
    public void RemoveLocation(WarehouseLocation item) => repository.RemoveLocation(item.Id);
    public void Remove(Warehouse item)
    {
        var impact = MasterDataImpactService.Warehouse(item.Id, inventoryRepository?.List() ?? Array.Empty<InventoryTransaction>());
        var decision = MasterDataImpactService.Decide(impact);
        if (!decision.CanDelete) throw new InvalidOperationException($"{decision.Reason}{decision.SuggestedAction}");
        repository.Remove(item.Id);
    }
    private void EnsureUnique(Warehouse item) { if (repository.List().Any(x => x.Id != item.Id && x.Code.Equals(item.Code, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("仓库编码已存在。"); }
    private void EnsureLocationUnique(Warehouse warehouse, WarehouseLocation item) { if (warehouse.Locations.Count(x => x.Code.Equals(item.Code, StringComparison.OrdinalIgnoreCase)) > 1) throw new InvalidOperationException("库位编码在该仓库中已存在。"); }
}
