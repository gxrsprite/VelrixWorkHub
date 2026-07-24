using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.Warehouses;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.Inventory;

public sealed record InventoryBalance(Guid ProductId, Guid WarehouseId, decimal Quantity);
public sealed record InventoryLocationBalance(Guid ProductId, Guid WarehouseId, Guid? LocationId, decimal Quantity);

public sealed class InventoryService(IInventoryTransactionRepository repository, IProductRepository productRepository, IWarehouseRepository warehouseRepository)
{
    public IReadOnlyList<InventoryTransaction> List(string? keyword = null, InventoryTransactionKind? kind = null, Guid? warehouseId = null)
    {
        var query = repository.List().AsEnumerable();
        var text = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(text)) query = query.Where(x => x.SourceNo.Contains(text, StringComparison.OrdinalIgnoreCase) || (x.Notes?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false));
        if (kind is not null) query = query.Where(x => x.Kind == kind);
        if (warehouseId is not null) query = query.Where(x => x.WarehouseId == warehouseId);
        return query.OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.SourceNo).ToArray();
    }
    public IReadOnlyList<InventoryBalance> Balances() => repository.List().GroupBy(x => new { x.ProductId, x.WarehouseId }).Select(x => new InventoryBalance(x.Key.ProductId, x.Key.WarehouseId, x.Sum(y => y.SignedQuantity))).OrderBy(x => x.ProductId).ToArray();
    public IReadOnlyList<InventoryLocationBalance> LocationBalances() => repository.List().GroupBy(x => new { x.ProductId, x.WarehouseId, x.LocationId }).Select(x => new InventoryLocationBalance(x.Key.ProductId, x.Key.WarehouseId, x.Key.LocationId, x.Sum(y => y.SignedQuantity))).OrderBy(x => x.ProductId).ThenBy(x => x.LocationId).ToArray();
    public InventoryTransaction Create(Guid productId, Guid warehouseId, InventoryTransactionKind kind, decimal quantity, string sourceNo, DateOnly date, string? notes, Guid? locationId = null, string? batchNo = null, DateOnly? expiryDate = null)
    {
        var product = productRepository.List().FirstOrDefault(x => x.Id == productId);
        if (product is null) throw new InvalidOperationException("商品不存在。");
        if (product.Status != ProductStatus.Active) throw new InvalidOperationException("商品已停用，不能登记库存流水。");
        var warehouse = warehouseRepository.List().FirstOrDefault(x => x.Id == warehouseId);
        if (warehouse is null) throw new InvalidOperationException("仓库不存在。");
        if (warehouse.Status != WarehouseStatus.Active) throw new InvalidOperationException("仓库已停用，不能登记库存流水。");
        if (locationId is not null && !warehouseRepository.List().SelectMany(x => x.Locations).Any(x => x.Id == locationId && x.WarehouseId == warehouseId)) throw new InvalidOperationException("库位不属于所选仓库。");
        if (kind == InventoryTransactionKind.Outbound)
        {
            var available = locationId is null
                ? Balances().FirstOrDefault(x => x.ProductId == productId && x.WarehouseId == warehouseId)?.Quantity ?? 0
                : LocationBalances().FirstOrDefault(x => x.ProductId == productId && x.WarehouseId == warehouseId && x.LocationId == locationId)?.Quantity ?? 0;
            if (available < quantity) throw new InvalidOperationException($"库存不足，当前可用库存为 {available:N2}。");
        }
        var item = new InventoryTransaction(productId, warehouseId, kind, quantity, sourceNo, date, notes, locationId, batchNo, expiryDate);
        if (repository.List().Any(x => x.SourceNo.Equals(item.SourceNo, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("库存流水单号已存在。");
        repository.Add(item); return item;
    }

    public void Transfer(Guid productId, Guid sourceWarehouseId, Guid? sourceLocationId, Guid targetWarehouseId, Guid? targetLocationId, decimal quantity, string transferNo, DateOnly date)
    {
        if (string.IsNullOrWhiteSpace(transferNo)) throw new ArgumentException("调拨单号不能为空。", nameof(transferNo));
        if (sourceWarehouseId == targetWarehouseId && sourceLocationId == targetLocationId) throw new InvalidOperationException("调出仓库和调入库位不能相同。");
        var warehouses = warehouseRepository.List();
        var source = warehouses.FirstOrDefault(x => x.Id == sourceWarehouseId) ?? throw new InvalidOperationException("调出仓库不存在。");
        var target = warehouses.FirstOrDefault(x => x.Id == targetWarehouseId) ?? throw new InvalidOperationException("调入仓库不存在。");
        if (source.Status != WarehouseStatus.Active) throw new InvalidOperationException("调出仓库已停用，不能调拨。");
        if (target.Status != WarehouseStatus.Active) throw new InvalidOperationException("调入仓库已停用，不能调拨。");
        if (sourceLocationId is not null && !source.Locations.Any(x => x.Id == sourceLocationId)) throw new InvalidOperationException("调出库位不属于调出仓库。");
        if (targetLocationId is not null && !target.Locations.Any(x => x.Id == targetLocationId)) throw new InvalidOperationException("调入库位不属于调入仓库。");
        if (repository.List().Any(x => x.SourceNo.Equals($"{transferNo}-OUT", StringComparison.OrdinalIgnoreCase) || x.SourceNo.Equals($"{transferNo}-IN", StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("调拨单号已存在。");
        Create(productId, sourceWarehouseId, InventoryTransactionKind.Outbound, quantity, $"{transferNo}-OUT", date, $"调拨至 {target.Code}", sourceLocationId);
        Create(productId, targetWarehouseId, InventoryTransactionKind.Inbound, quantity, $"{transferNo}-IN", date, $"从 {source.Code} 调拨", targetLocationId);
    }

    public InventoryTransaction Stocktake(Guid productId, Guid warehouseId, decimal actualQuantity, string sourceNo, DateOnly date, Guid? locationId = null)
    {
        if (actualQuantity < 0) throw new ArgumentOutOfRangeException(nameof(actualQuantity), "盘点数量不能为负数。");
        if (locationId is not null && !warehouseRepository.List().Any(x => x.Id == warehouseId && x.Locations.Any(y => y.Id == locationId))) throw new InvalidOperationException("盘点库位不属于所选仓库。");
        var bookQuantity = locationId is null
            ? Balances().FirstOrDefault(x => x.ProductId == productId && x.WarehouseId == warehouseId)?.Quantity ?? 0
            : LocationBalances().FirstOrDefault(x => x.ProductId == productId && x.WarehouseId == warehouseId && x.LocationId == locationId)?.Quantity ?? 0;
        var difference = decimal.Round(actualQuantity - bookQuantity, 2);
        if (difference == 0) throw new InvalidOperationException("盘点数量与账面余额一致，无需生成调整流水。");
        return Create(productId, warehouseId, InventoryTransactionKind.Adjustment, difference, sourceNo, date, $"盘点调整：账面 {bookQuantity:N2}，实盘 {actualQuantity:N2}", locationId);
    }
}
