using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.Warehouses;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

/// <summary>
/// MOM-08J 维修备件消耗。只允许进行中的维修工单写入，库存变化统一通过 ERP InventoryService，
/// 维修消耗事实与 ERP 出库流水共享事务并使用业务单号幂等。
/// </summary>
public sealed class MomServiceWorkOrderPartConsumptionService(
    IMomServiceWorkOrderPartConsumptionRepository repository,
    MomServiceWorkOrderService workOrderService,
    InventoryService inventoryService,
    ProductService productService,
    WarehouseService warehouseService,
    IWorkflowTransactionBoundary? transactions = null)
{
    public IReadOnlyList<MomServiceWorkOrderPartConsumption> List(Guid? serviceWorkOrderId = null)
        => repository.List(serviceWorkOrderId).OrderByDescending(x => x.ConsumedOn).ThenByDescending(x => x.SourceNo).ToArray();

    public MomServiceWorkOrderPartConsumption Create(Guid serviceWorkOrderId, Guid productId, Guid warehouseId,
        Guid? locationId, decimal quantity, string sourceNo, DateOnly consumedOn, string actor,
        string? batchNo = null, DateOnly? expiryDate = null, string? serialNo = null, string? notes = null,
        string? otherInfo = null)
    {
        var workOrder = workOrderService.Get(serviceWorkOrderId) ?? throw new InvalidOperationException("服务工单不存在。 ");
        if (workOrder.Type != MomServiceWorkOrderType.Repair) throw new InvalidOperationException("备件消耗只能绑定维修工单。 ");
        if (workOrder.Status != MomServiceWorkOrderStatus.InProgress) throw new InvalidOperationException("只有进行中的维修工单可以登记备件消耗。 ");

        var existing = repository.List(serviceWorkOrderId).FirstOrDefault(x => x.SourceNo.Equals(sourceNo?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            EnsureSameRequest(existing, productId, warehouseId, locationId, quantity, consumedOn, batchNo, expiryDate, serialNo);
            return existing;
        }
        if (repository.List().Any(x => x.SourceNo.Equals(sourceNo?.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("备件消耗单号已存在。 ");
        if (inventoryService.List(sourceNo).Any(x => x.SourceNo.Equals(sourceNo?.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("库存流水已存在但缺少维修备件消耗记录。 ");

        EnsureProduct(productId); EnsureWarehouse(warehouseId, locationId);
        EnsureInventoryDimensions(productId, warehouseId, locationId, quantity, consumedOn, batchNo, expiryDate, serialNo);
        var item = new MomServiceWorkOrderPartConsumption(serviceWorkOrderId, workOrder.EquipmentId, productId,
            warehouseId, locationId, quantity, sourceNo, consumedOn, batchNo, expiryDate, serialNo, actor, notes, otherInfo);
        void Persist()
        {
            inventoryService.Create(productId, warehouseId, InventoryTransactionKind.Outbound, item.Quantity, item.SourceNo,
                consumedOn, item.Notes ?? $"维修工单 {workOrder.WorkOrderNo} 备件消耗", locationId, item.BatchNo, item.ExpiryDate, item.SerialNo);
            repository.Add(item);
        }
        if (transactions is null) Persist(); else transactions.Execute(Persist);
        return item;
    }

    private void EnsureProduct(Guid productId)
    {
        var product = productService.List().FirstOrDefault(x => x.Id == productId)
            ?? throw new InvalidOperationException("备件商品不存在。 ");
        if (product.Status != ProductStatus.Active) throw new InvalidOperationException("备件商品已停用，不能登记消耗。 ");
    }

    private void EnsureWarehouse(Guid warehouseId, Guid? locationId)
    {
        var warehouse = warehouseService.List().FirstOrDefault(x => x.Id == warehouseId)
            ?? throw new InvalidOperationException("备件仓库不存在。 ");
        if (warehouse.Status != WarehouseStatus.Active) throw new InvalidOperationException("备件仓库已停用，不能登记消耗。 ");
        if (locationId is Guid id && warehouse.Locations.All(x => x.Id != id)) throw new InvalidOperationException("备件库位不属于所选仓库。 ");
    }

    private void EnsureInventoryDimensions(Guid productId, Guid warehouseId, Guid? locationId, decimal quantity,
        DateOnly consumedOn, string? batchNo, DateOnly? expiryDate, string? serialNo)
    {
        if (expiryDate is DateOnly expiry && expiry < consumedOn) throw new InvalidOperationException("维修备件保质期不能早于消耗日期。 ");
        if (!string.IsNullOrWhiteSpace(serialNo) && decimal.Round(quantity, 2, MidpointRounding.AwayFromZero) != 1m)
            throw new InvalidOperationException("带序列号的维修备件数量必须为 1。 ");

        var normalizedBatch = Clean(batchNo);
        var normalizedSerial = Clean(serialNo);
        var available = !string.IsNullOrWhiteSpace(normalizedSerial)
            ? inventoryService.SerialBalances().Where(x => x.ProductId == productId && x.WarehouseId == warehouseId && x.LocationId == locationId && x.SerialNo.Equals(normalizedSerial, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Quantity)
            : string.IsNullOrWhiteSpace(normalizedBatch)
                ? locationId is null
                    ? inventoryService.Balances().FirstOrDefault(x => x.ProductId == productId && x.WarehouseId == warehouseId)?.Quantity ?? 0m
                    : inventoryService.LocationBalances().FirstOrDefault(x => x.ProductId == productId && x.WarehouseId == warehouseId && x.LocationId == locationId)?.Quantity ?? 0m
                : inventoryService.BatchBalances().Where(x => x.ProductId == productId && x.WarehouseId == warehouseId && x.LocationId == locationId && x.BatchNo.Equals(normalizedBatch, StringComparison.OrdinalIgnoreCase) && (expiryDate is null || x.ExpiryDate == expiryDate)).Sum(x => x.Quantity);
        if (available < decimal.Round(quantity, 2, MidpointRounding.AwayFromZero)) throw new InvalidOperationException("维修备件库存不足。 ");
    }

    private static void EnsureSameRequest(MomServiceWorkOrderPartConsumption existing, Guid productId, Guid warehouseId,
        Guid? locationId, decimal quantity, DateOnly consumedOn, string? batchNo, DateOnly? expiryDate, string? serialNo)
    {
        if (existing.ProductId != productId || existing.WarehouseId != warehouseId || existing.LocationId != locationId
            || existing.Quantity != decimal.Round(quantity, 2, MidpointRounding.AwayFromZero) || existing.ConsumedOn != consumedOn
            || !string.Equals(existing.BatchNo, Clean(batchNo), StringComparison.OrdinalIgnoreCase)
            || existing.ExpiryDate != expiryDate || !string.Equals(existing.SerialNo, Clean(serialNo), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("同一备件消耗单号不能重复绑定不同的库存请求。 ");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
