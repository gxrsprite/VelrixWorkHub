using FreeSql;
using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Inventory;
public sealed class FreeSqlInventoryTransactionRepository(IFreeSql fsql) : IInventoryTransactionRepository
{
    public IReadOnlyList<InventoryTransaction> List() => fsql.Select<InventoryTransactionRecord>().ToList().Select(x => new InventoryTransaction(x.ProductId, x.WarehouseId, x.Kind, x.Quantity, x.SourceNo, DateOnly.FromDateTime(x.OccurredOn), x.Notes, x.LocationId, x.BatchNo, x.ExpiryDate is null ? null : DateOnly.FromDateTime(x.ExpiryDate.Value)) { Id = x.Id }).ToArray();
    public void Add(InventoryTransaction item) => fsql.Insert(new InventoryTransactionRecord { Id = item.Id, ProductId = item.ProductId, WarehouseId = item.WarehouseId, LocationId = item.LocationId, Kind = item.Kind, Quantity = item.Quantity, SourceNo = item.SourceNo, OccurredOn = item.OccurredOn.ToDateTime(TimeOnly.MinValue), Notes = item.Notes, BatchNo = item.BatchNo, ExpiryDate = item.ExpiryDate?.ToDateTime(TimeOnly.MinValue) }).ExecuteAffrows();
}
