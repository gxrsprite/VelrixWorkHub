using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.Warehouses;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.Inventory;

public sealed record InventoryBalance(Guid ProductId, Guid WarehouseId, decimal Quantity);
public sealed record InventoryLocationBalance(Guid ProductId, Guid WarehouseId, Guid? LocationId, decimal Quantity);
public sealed record InventoryBatchBalance(Guid ProductId, Guid WarehouseId, Guid? LocationId, string BatchNo, DateOnly? ExpiryDate, decimal Quantity);
public sealed record InventoryBatchAllocation(string BatchNo, DateOnly? ExpiryDate, decimal Quantity, string SourceNo);
public sealed record InventorySerialBalance(Guid ProductId, Guid WarehouseId, Guid? LocationId, string SerialNo, decimal Quantity);
public sealed record InventoryOverstockAlert(Guid ProductId, string ProductName, decimal Quantity, decimal MaxInventoryQuantity);
public sealed record InventoryBatchExpiryAlert(Guid ProductId, Guid WarehouseId, Guid? LocationId, string BatchNo, DateOnly ExpiryDate, decimal Quantity, bool IsExpired);
public sealed record InventoryBatchStagnancyAlert(Guid ProductId, Guid WarehouseId, Guid? LocationId, string BatchNo, DateOnly? ExpiryDate, decimal Quantity, DateOnly LastOccurredOn);

public sealed class InventoryService(IInventoryTransactionRepository repository, IProductRepository productRepository, IWarehouseRepository warehouseRepository, IWorkflowTransactionBoundary? transactions = null)
{
    public IReadOnlyList<InventoryTransaction> List(string? keyword = null, InventoryTransactionKind? kind = null, Guid? warehouseId = null, string? batchNo = null, string? serialNo = null)
    {
        var query = repository.List().AsEnumerable();
        var text = keyword?.Trim();
        var normalizedBatchNo = batchNo?.Trim();
        var normalizedSerialNo = serialNo?.Trim();
        if (!string.IsNullOrWhiteSpace(text)) query = query.Where(x => x.SourceNo.Contains(text, StringComparison.OrdinalIgnoreCase) || (x.Notes?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false));
        if (kind is not null) query = query.Where(x => x.Kind == kind);
        if (warehouseId is not null) query = query.Where(x => x.WarehouseId == warehouseId);
        if (!string.IsNullOrWhiteSpace(normalizedBatchNo)) query = query.Where(x => x.BatchNo?.Equals(normalizedBatchNo, StringComparison.OrdinalIgnoreCase) == true);
        if (!string.IsNullOrWhiteSpace(normalizedSerialNo)) query = query.Where(x => x.SerialNo?.Equals(normalizedSerialNo, StringComparison.OrdinalIgnoreCase) == true);
        return query.OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.SourceNo).ToArray();
    }
    public IReadOnlyList<InventoryBalance> Balances() => repository.List().GroupBy(x => new { x.ProductId, x.WarehouseId }).Select(x => new InventoryBalance(x.Key.ProductId, x.Key.WarehouseId, x.Sum(y => y.SignedQuantity))).OrderBy(x => x.ProductId).ToArray();
    public IReadOnlyList<InventoryLocationBalance> LocationBalances() => repository.List().GroupBy(x => new { x.ProductId, x.WarehouseId, x.LocationId }).Select(x => new InventoryLocationBalance(x.Key.ProductId, x.Key.WarehouseId, x.Key.LocationId, x.Sum(y => y.SignedQuantity))).OrderBy(x => x.ProductId).ThenBy(x => x.LocationId).ToArray();
    public IReadOnlyList<InventoryBatchBalance> BatchBalances() => repository.List().Where(x => !string.IsNullOrWhiteSpace(x.BatchNo)).GroupBy(x => new { x.ProductId, x.WarehouseId, x.LocationId, x.BatchNo, x.ExpiryDate }).Select(x => new InventoryBatchBalance(x.Key.ProductId, x.Key.WarehouseId, x.Key.LocationId, x.Key.BatchNo!, x.Key.ExpiryDate, x.Sum(y => y.SignedQuantity))).Where(x => x.Quantity != 0).OrderBy(x => x.ProductId).ThenBy(x => x.BatchNo).ThenBy(x => x.ExpiryDate).ToArray();
    public IReadOnlyList<InventorySerialBalance> SerialBalances() => repository.List().Where(x => !string.IsNullOrWhiteSpace(x.SerialNo)).GroupBy(x => new { x.ProductId, x.WarehouseId, x.LocationId, x.SerialNo }).Select(x => new InventorySerialBalance(x.Key.ProductId, x.Key.WarehouseId, x.Key.LocationId, x.Key.SerialNo!, x.Sum(y => y.SignedQuantity))).Where(x => x.Quantity != 0).OrderBy(x => x.ProductId).ThenBy(x => x.SerialNo, StringComparer.OrdinalIgnoreCase).ToArray();
    public IReadOnlyList<InventoryOverstockAlert> OverstockAlerts() => productRepository.List()
        .Where(x => x.Status == ProductStatus.Active && x.MaxInventoryQuantity is > 0)
        .Select(x => new InventoryOverstockAlert(x.Id, x.Name, Balances().Where(y => y.ProductId == x.Id).Sum(y => y.Quantity), x.MaxInventoryQuantity!.Value))
        .Where(x => x.Quantity > x.MaxInventoryQuantity)
        .OrderByDescending(x => x.Quantity - x.MaxInventoryQuantity)
        .ThenBy(x => x.ProductName)
        .ToArray();
    public IReadOnlyList<InventoryBatchExpiryAlert> ExpiryAlerts(DateOnly referenceDate, int withinDays = 30)
    {
        if (withinDays is < 0 or > 365) throw new ArgumentOutOfRangeException(nameof(withinDays), "预警窗口必须在 0 到 365 天之间。");
        var deadline = referenceDate.AddDays(withinDays);
        return BatchBalances()
            .Where(x => x.Quantity > 0 && x.ExpiryDate is DateOnly expiry && expiry <= deadline)
            .Select(x => new InventoryBatchExpiryAlert(x.ProductId, x.WarehouseId, x.LocationId, x.BatchNo, x.ExpiryDate!.Value, x.Quantity, x.ExpiryDate.Value < referenceDate))
            .OrderByDescending(x => x.IsExpired)
            .ThenBy(x => x.ExpiryDate)
            .ThenBy(x => x.BatchNo)
            .ToArray();
    }
    public IReadOnlyList<InventoryBatchStagnancyAlert> StagnantBatchAlerts(DateOnly referenceDate, int inactiveDays = 180)
    {
        if (inactiveDays is < 1 or > 3650) throw new ArgumentOutOfRangeException(nameof(inactiveDays), "呆滞阈值必须在 1 到 3650 天之间。");
        var cutoff = referenceDate.AddDays(-inactiveDays);
        return repository.List()
            .Where(x => !string.IsNullOrWhiteSpace(x.BatchNo))
            .GroupBy(x => new { x.ProductId, x.WarehouseId, x.LocationId, x.BatchNo, x.ExpiryDate })
            .Select(x => new InventoryBatchStagnancyAlert(x.Key.ProductId, x.Key.WarehouseId, x.Key.LocationId, x.Key.BatchNo!, x.Key.ExpiryDate, x.Sum(y => y.SignedQuantity), x.Max(y => y.OccurredOn)))
            .Where(x => x.Quantity > 0 && x.LastOccurredOn <= cutoff)
            .OrderBy(x => x.LastOccurredOn)
            .ThenBy(x => x.BatchNo)
            .ToArray();
    }
    public InventoryTransaction Create(Guid productId, Guid warehouseId, InventoryTransactionKind kind, decimal quantity, string sourceNo, DateOnly date, string? notes, Guid? locationId = null, string? batchNo = null, DateOnly? expiryDate = null, string? serialNo = null)
    {
        var product = productRepository.List().FirstOrDefault(x => x.Id == productId);
        if (product is null) throw new InvalidOperationException("商品不存在。");
        if (product.Status != ProductStatus.Active) throw new InvalidOperationException("商品已停用，不能登记库存流水。");
        var warehouse = warehouseRepository.List().FirstOrDefault(x => x.Id == warehouseId);
        if (warehouse is null) throw new InvalidOperationException("仓库不存在。");
        if (warehouse.Status != WarehouseStatus.Active) throw new InvalidOperationException("仓库已停用，不能登记库存流水。");
        if (locationId is not null && !warehouseRepository.List().SelectMany(x => x.Locations).Any(x => x.Id == locationId && x.WarehouseId == warehouseId)) throw new InvalidOperationException("库位不属于所选仓库。");
        EnsureLocationProductCapacity(warehouse, locationId, productId, kind, quantity);
        var normalizedSerial = serialNo?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSerial))
        {
            var currentSerialBalance = SerialBalances().FirstOrDefault(x => x.ProductId == productId && x.WarehouseId == warehouseId && x.LocationId == locationId && x.SerialNo.Equals(normalizedSerial, StringComparison.OrdinalIgnoreCase))?.Quantity ?? 0;
            var serialExistsElsewhere = SerialBalances().Any(x => x.ProductId == productId && x.SerialNo.Equals(normalizedSerial, StringComparison.OrdinalIgnoreCase) && x.Quantity > 0);
            if (kind == InventoryTransactionKind.Outbound || kind == InventoryTransactionKind.Adjustment && quantity < 0)
            {
                if (currentSerialBalance < Math.Abs(quantity)) throw new InvalidOperationException($"序列号“{normalizedSerial}”不在所选仓库或库位，不能出库。" );
            }
            else if (serialExistsElsewhere) throw new InvalidOperationException($"序列号“{normalizedSerial}”已有在库记录，不能重复入库。" );
        }
        if (kind == InventoryTransactionKind.Outbound)
        {
            var normalizedBatch = batchNo?.Trim();
            var available = !string.IsNullOrWhiteSpace(normalizedSerial)
                ? SerialBalances().Where(x => x.ProductId == productId && x.WarehouseId == warehouseId && x.LocationId == locationId && x.SerialNo.Equals(normalizedSerial, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Quantity)
                : string.IsNullOrWhiteSpace(normalizedBatch)
                ? locationId is null
                    ? Balances().FirstOrDefault(x => x.ProductId == productId && x.WarehouseId == warehouseId)?.Quantity ?? 0
                    : LocationBalances().FirstOrDefault(x => x.ProductId == productId && x.WarehouseId == warehouseId && x.LocationId == locationId)?.Quantity ?? 0
                : BatchBalances().Where(x => x.ProductId == productId && x.WarehouseId == warehouseId && x.LocationId == locationId && x.BatchNo.Equals(normalizedBatch, StringComparison.OrdinalIgnoreCase) && (expiryDate is null || x.ExpiryDate == expiryDate)).Sum(x => x.Quantity);
            if (available < quantity) throw new InvalidOperationException(!string.IsNullOrWhiteSpace(normalizedSerial) ? $"序列号“{normalizedSerial}”库存不足。" : string.IsNullOrWhiteSpace(normalizedBatch) ? $"库存不足，当前可用库存为 {available:N2}。" : $"批次“{normalizedBatch}”库存不足，当前可用库存为 {available:N2}。");
        }
        var item = new InventoryTransaction(productId, warehouseId, kind, quantity, sourceNo, date, notes, locationId, batchNo, expiryDate, serialNo);
        if (repository.List().Any(x => x.SourceNo.Equals(item.SourceNo, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("库存流水单号已存在。");
        repository.Add(item); return item;
    }

    public IReadOnlyList<InventoryBatchAllocation> OutboundByFifo(Guid productId, Guid warehouseId, Guid? locationId, decimal quantity, string sourceNo, DateOnly date, string? notes)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "出库数量必须大于零。");
        var normalizedSourceNo = sourceNo?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedSourceNo)) throw new ArgumentException("库存流水单号不能为空。", nameof(sourceNo));
        if (normalizedSourceNo.Length > 76) throw new ArgumentException("FIFO 出库单号最多 76 个字符。", nameof(sourceNo));

        var batches = BatchBalances()
            .Where(x => x.ProductId == productId && x.WarehouseId == warehouseId && x.LocationId == locationId && x.Quantity > 0)
            .OrderBy(x => x.ExpiryDate ?? DateOnly.MaxValue)
            .ThenBy(x => x.BatchNo, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var remaining = quantity;
        var allocations = new List<InventoryBatchAllocation>();
        foreach (var batch in batches)
        {
            if (remaining <= 0) break;
            var allocatedQuantity = Math.Min(remaining, batch.Quantity);
            var childSourceNo = $"{normalizedSourceNo}-B{allocations.Count + 1:D2}";
            allocations.Add(new InventoryBatchAllocation(batch.BatchNo, batch.ExpiryDate, allocatedQuantity, childSourceNo));
            remaining -= allocatedQuantity;
        }

        if (remaining > 0) throw new InvalidOperationException($"可按批次先进先出出库的库存不足，当前可用库存为 {(quantity - remaining):N2}。");
        if (repository.List().Any(x => allocations.Any(y => x.SourceNo.Equals(y.SourceNo, StringComparison.OrdinalIgnoreCase)))) throw new InvalidOperationException("库存流水单号已存在。");

        void OutboundCore()
        {
            foreach (var allocation in allocations)
            {
                Create(productId, warehouseId, InventoryTransactionKind.Outbound, allocation.Quantity, allocation.SourceNo, date, notes, locationId, allocation.BatchNo, allocation.ExpiryDate);
            }
        }

        if (transactions is null) OutboundCore();
        else transactions.Execute(OutboundCore);
        return allocations;
    }

    public void Transfer(Guid productId, Guid sourceWarehouseId, Guid? sourceLocationId, Guid targetWarehouseId, Guid? targetLocationId, decimal quantity, string transferNo, DateOnly date, string? batchNo = null, DateOnly? expiryDate = null, string? serialNo = null)
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
        EnsureLocationProductCapacity(target, targetLocationId, productId, InventoryTransactionKind.Inbound, quantity);
        void TransferCore()
        {
            Create(productId, sourceWarehouseId, InventoryTransactionKind.Outbound, quantity, $"{transferNo}-OUT", date, $"调拨至 {target.Code}", sourceLocationId, batchNo, expiryDate, serialNo);
            Create(productId, targetWarehouseId, InventoryTransactionKind.Inbound, quantity, $"{transferNo}-IN", date, $"从 {source.Code} 调拨", targetLocationId, batchNo, expiryDate, serialNo);
        }
        if (transactions is null) TransferCore();
        else transactions.Execute(TransferCore);
    }

    public InventoryTransaction Stocktake(Guid productId, Guid warehouseId, decimal actualQuantity, string sourceNo, DateOnly date, Guid? locationId = null, string? batchNo = null, DateOnly? expiryDate = null, string? serialNo = null)
    {
        if (actualQuantity < 0) throw new ArgumentOutOfRangeException(nameof(actualQuantity), "盘点数量不能为负数。");
        if (locationId is not null && !warehouseRepository.List().Any(x => x.Id == warehouseId && x.Locations.Any(y => y.Id == locationId))) throw new InvalidOperationException("盘点库位不属于所选仓库。");
        var normalizedBatch = batchNo?.Trim();
        var normalizedSerial = serialNo?.Trim();
        var bookQuantity = !string.IsNullOrWhiteSpace(normalizedSerial)
            ? SerialBalances().Where(x => x.ProductId == productId && x.WarehouseId == warehouseId && x.LocationId == locationId && x.SerialNo.Equals(normalizedSerial, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Quantity)
            : string.IsNullOrWhiteSpace(normalizedBatch)
            ? locationId is null
                ? Balances().FirstOrDefault(x => x.ProductId == productId && x.WarehouseId == warehouseId)?.Quantity ?? 0
                : LocationBalances().FirstOrDefault(x => x.ProductId == productId && x.WarehouseId == warehouseId && x.LocationId == locationId)?.Quantity ?? 0
            : BatchBalances().Where(x => x.ProductId == productId && x.WarehouseId == warehouseId && x.LocationId == locationId && x.BatchNo.Equals(normalizedBatch, StringComparison.OrdinalIgnoreCase) && (expiryDate is null || x.ExpiryDate == expiryDate)).Sum(x => x.Quantity);
        var difference = decimal.Round(actualQuantity - bookQuantity, 2);
        if (difference == 0) throw new InvalidOperationException("盘点数量与账面余额一致，无需生成调整流水。");
        return Create(productId, warehouseId, InventoryTransactionKind.Adjustment, difference, sourceNo, date, $"盘点调整：账面 {bookQuantity:N2}，实盘 {actualQuantity:N2}", locationId, batchNo, expiryDate, serialNo);
    }
    private void EnsureLocationProductCapacity(Warehouse warehouse, Guid? locationId, Guid productId, InventoryTransactionKind kind, decimal quantity)
    {
        if (locationId is null || (kind == InventoryTransactionKind.Outbound || kind == InventoryTransactionKind.Adjustment && quantity <= 0)) return;
        var capacity = warehouse.Locations.Single(x => x.Id == locationId).ProductCapacities.SingleOrDefault(x => x.ProductId == productId);
        if (capacity is null) return;
        var current = LocationBalances().FirstOrDefault(x => x.ProductId == productId && x.WarehouseId == warehouse.Id && x.LocationId == locationId)?.Quantity ?? 0m;
        if (current + quantity > capacity.MaxQuantity) throw new InvalidOperationException($"库位“{warehouse.Locations.Single(x => x.Id == locationId).Code}”的该商品容量为 {capacity.MaxQuantity:N2}，当前账面 {current:N2}，本次登记后将超出容量。");
    }
}
