using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

/// <summary>
/// MOM-08A 完工入库。制造工单只负责完工数量，库存余额统一由 ERP InventoryService 维护。
/// </summary>
public sealed class MomFinishedGoodsReceiptService(
    IMomFinishedGoodsReceiptRepository repository,
    IMomWorkOrderRepository workOrderRepository,
    IInventoryTransactionRepository inventoryRepository,
    InventoryService inventoryService,
    IWorkflowTransactionBoundary? transactions = null)
{
    public IReadOnlyList<MomFinishedGoodsReceipt> List(Guid? workOrderId = null)
    {
        var query = repository.List().AsEnumerable();
        if (workOrderId is Guid selected) query = query.Where(x => x.WorkOrderId == selected);
        return query.OrderByDescending(x => x.ReceiptDate).ThenByDescending(x => x.SourceNo).ToArray();
    }

    public decimal ReceivedQuantity(Guid workOrderId) => List(workOrderId).Sum(x => x.Quantity);

    public MomFinishedGoodsReceipt Create(Guid workOrderId, Guid warehouseId, Guid? locationId, decimal quantity,
        string sourceNo, DateOnly receiptDate, string? batchNo = null, DateOnly? expiryDate = null,
        string? serialNo = null, string? otherInfo = null)
    {
        var workOrder = workOrderRepository.List().FirstOrDefault(x => x.Id == workOrderId)
            ?? throw new InvalidOperationException("制造工单不存在。");
        if (workOrder.Status != MomWorkOrderStatus.Completed)
            throw new InvalidOperationException("只有已完工制造工单可以登记完工入库。");
        var remaining = workOrder.CompletedQuantity - ReceivedQuantity(workOrderId);
        if (quantity > remaining)
            throw new InvalidOperationException("完工入库数量不能超过未入库完工数量。");
        var normalizedSourceNo = sourceNo?.Trim() ?? string.Empty;
        if (repository.List().Any(x => x.SourceNo.Equals(normalizedSourceNo, StringComparison.OrdinalIgnoreCase))
            || inventoryRepository.List().Any(x => x.SourceNo.Equals(normalizedSourceNo, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("完工入库来源单号已存在。");
        var item = new MomFinishedGoodsReceipt(workOrder.Id, workOrder.ProductId, warehouseId, locationId, quantity, normalizedSourceNo,
            receiptDate, batchNo, expiryDate, serialNo, otherInfo);

        void Core()
        {
            inventoryService.Create(item.ProductId, item.WarehouseId, InventoryTransactionKind.Inbound, item.Quantity, item.SourceNo,
                item.ReceiptDate, $"制造工单 {workOrder.WorkOrderNo} 完工入库", item.LocationId, item.BatchNo, item.ExpiryDate, item.SerialNo);
            repository.Add(item);
        }

        if (transactions is null) Core(); else transactions.Execute(Core);
        return item;
    }
}
