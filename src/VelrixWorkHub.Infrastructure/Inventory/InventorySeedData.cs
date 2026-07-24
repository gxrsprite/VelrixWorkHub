using FreeSql;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Inventory;
public static class InventorySeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<InventoryTransactionRecord>();
        if (fsql.Select<InventoryTransactionRecord>().Any()) return;
        var product = fsql.Select<VelrixWorkHub.Infrastructure.Products.ProductRecord>().First(); var warehouse = fsql.Select<VelrixWorkHub.Infrastructure.Warehouses.WarehouseRecord>().First();
        if (product is null || warehouse is null) return;
        var item = new InventoryTransaction(product.Id, warehouse.Id, InventoryTransactionKind.Inbound, 25, "INV-20260712-001", DateOnly.FromDateTime(DateTime.Today), "期初库存");
        fsql.Insert(new InventoryTransactionRecord { Id = item.Id, ProductId = item.ProductId, WarehouseId = item.WarehouseId, Kind = item.Kind, Quantity = item.Quantity, SourceNo = item.SourceNo, OccurredOn = item.OccurredOn.ToDateTime(TimeOnly.MinValue), Notes = item.Notes }).ExecuteAffrows();
    }
}
