using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Application.SalesOrders;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public sealed record MomFinishedGoodsShipmentAllocationRequest(Guid FinishedGoodsReceiptId, decimal Quantity);

/// <summary>
/// MOM-08D: shipment execution with atomic multi-source allocation.
/// Historical single-source rows remain valid; new multi-source shipments use immutable allocation rows.
/// </summary>
public sealed class MomFinishedGoodsShipmentService(
    IMomFinishedGoodsShipmentRepository repository,
    IMomFinishedGoodsShipmentAllocationRepository allocationRepository,
    IMomFinishedGoodsReceiptRepository receiptRepository,
    ISalesOrderRepository salesOrderRepository,
    ISalesOrderShipmentService salesOrderShipmentService,
    IInventoryTransactionRepository inventoryRepository,
    InventoryService inventoryService,
    IWorkflowTransactionBoundary? transactions = null)
{
    public IReadOnlyList<MomFinishedGoodsShipment> List(Guid? salesOrderId = null)
    {
        var query = repository.List().AsEnumerable();
        if (salesOrderId is Guid selected) query = query.Where(x => x.SalesOrderId == selected);
        return query.OrderByDescending(x => x.ShipmentDate).ThenByDescending(x => x.SourceNo).ToArray();
    }

    public IReadOnlyList<MomFinishedGoodsShipmentAllocation> ListAllocations(Guid? shipmentId = null)
        => allocationRepository.List(shipmentId).OrderBy(x => x.SourceNo).ToArray();

    public MomFinishedGoodsShipment? FindBySalesOrder(Guid salesOrderId) => List(salesOrderId).FirstOrDefault();

    public decimal ShippedQuantity(Guid salesOrderId) => List(salesOrderId).Sum(EffectiveShipmentQuantity);

    public decimal RemainingQuantity(Guid salesOrderId)
    {
        var order = salesOrderRepository.List().FirstOrDefault(x => x.Id == salesOrderId)
            ?? throw new InvalidOperationException("销售订单不存在。");
        return Math.Max(0m, order.Quantity - ShippedQuantity(salesOrderId));
    }

    public decimal ReceiptRemainingQuantity(Guid finishedGoodsReceiptId)
    {
        var receipt = receiptRepository.List().FirstOrDefault(x => x.Id == finishedGoodsReceiptId)
            ?? throw new InvalidOperationException("完工入库记录不存在。");
        var shipped = ShippedFromReceipt(finishedGoodsReceiptId);
        return Math.Max(0m, receipt.Quantity - shipped);
    }

    public MomFinishedGoodsShipment Create(Guid salesOrderId, Guid finishedGoodsReceiptId, DateOnly shipmentDate, string? otherInfo = null)
        => CreateInternal(salesOrderId, finishedGoodsReceiptId, shipmentDate, null, otherInfo);

    public MomFinishedGoodsShipment Create(Guid salesOrderId, Guid finishedGoodsReceiptId, DateOnly shipmentDate, decimal quantity, string? otherInfo = null)
        => CreateInternal(salesOrderId, finishedGoodsReceiptId, shipmentDate, quantity, otherInfo);

    /// <summary>
    /// Creates one shipment event from multiple completed-goods receipts. All allocations,
    /// ERP outbound transactions and the final sales-order status transition share one transaction.
    /// </summary>
    public MomFinishedGoodsShipment CreateFromReceipts(Guid salesOrderId, DateOnly shipmentDate,
        IReadOnlyList<MomFinishedGoodsShipmentAllocationRequest> requests, string? otherInfo = null)
    {
        if (requests is null || requests.Count == 0) throw new InvalidOperationException("至少选择一个完工入库来源。");
        if (requests.Any(x => x.FinishedGoodsReceiptId == Guid.Empty)) throw new InvalidOperationException("完工入库来源不能为空。");
        if (requests.GroupBy(x => x.FinishedGoodsReceiptId).Any(x => x.Count() > 1)) throw new InvalidOperationException("同一发运单不能重复选择同一个完工入库来源。");

        var order = GetSubmittedOrder(salesOrderId);
        var orderRemaining = order.Quantity - ShippedQuantity(order.Id);
        if (orderRemaining <= 0) throw new InvalidOperationException("该销售订单已完成发运。");

        var normalized = requests.Select(x => new MomFinishedGoodsShipmentAllocationRequest(
            x.FinishedGoodsReceiptId, decimal.Round(x.Quantity, 6, MidpointRounding.AwayFromZero))).ToArray();
        if (normalized.Any(x => x.Quantity <= 0)) throw new InvalidOperationException("来源分配数量必须大于零。");
        var total = normalized.Sum(x => x.Quantity);
        if (total > orderRemaining) throw new InvalidOperationException("来源分配总量不能超过销售订单剩余数量。");

        var receipts = normalized.Select(x => receiptRepository.List().FirstOrDefault(r => r.Id == x.FinishedGoodsReceiptId)
            ?? throw new InvalidOperationException("完工入库记录不存在。"))
            .ToArray();
        for (var index = 0; index < normalized.Length; index++)
        {
            var receipt = receipts[index];
            if (receipt.ProductId != order.ProductId) throw new InvalidOperationException("完工入库商品必须与销售订单商品一致。");
            if (normalized[index].Quantity > ReceiptRemainingQuantity(receipt.Id)) throw new InvalidOperationException("完工入库可用数量不足，不能发运。");
        }

        var shipmentSourceNo = NextSourceNo(order.OrderNo);
        var primary = receipts[0];
        var shipment = new MomFinishedGoodsShipment(order.Id, primary.Id, order.ProductId, primary.WarehouseId,
            primary.LocationId, total, shipmentSourceNo, shipmentDate, otherInfo: otherInfo);
        var allocationItems = normalized.Select((request, index) =>
        {
            var receipt = receipts[index];
            return new MomFinishedGoodsShipmentAllocation(shipment.Id, receipt.Id, order.ProductId, receipt.WarehouseId,
                receipt.LocationId, request.Quantity, AllocationSourceNo(shipmentSourceNo, index + 1), shipmentDate,
                receipt.BatchNo, receipt.ExpiryDate, receipt.SerialNo, otherInfo);
        }).ToArray();
        var completesOrder = ShippedQuantity(order.Id) + shipment.Quantity >= order.Quantity;

        void Core()
        {
            foreach (var allocation in allocationItems)
            {
                inventoryService.Create(allocation.ProductId, allocation.WarehouseId, InventoryTransactionKind.Outbound,
                    allocation.Quantity, allocation.SourceNo, shipmentDate, $"销售订单 {order.OrderNo} 完工成品多来源发运",
                    allocation.LocationId, allocation.BatchNo, allocation.ExpiryDate, allocation.SerialNo);
            }
            if (completesOrder) salesOrderShipmentService.ConfirmShipped(order);
            repository.Add(shipment);
            foreach (var allocation in allocationItems) allocationRepository.Add(allocation);
        }

        if (transactions is null) Core();
        else transactions.Execute(Core, _ => salesOrderShipmentService.RestoreSubmittedAfterRollback(order));
        return shipment;
    }

    private MomFinishedGoodsShipment CreateInternal(Guid salesOrderId, Guid finishedGoodsReceiptId, DateOnly shipmentDate,
        decimal? requestedQuantity, string? otherInfo)
    {
        var order = GetSubmittedOrder(salesOrderId);
        var receipt = receiptRepository.List().FirstOrDefault(x => x.Id == finishedGoodsReceiptId)
            ?? throw new InvalidOperationException("完工入库记录不存在。");
        if (receipt.ProductId != order.ProductId) throw new InvalidOperationException("完工入库商品必须与销售订单商品一致。");

        var orderShipped = ShippedQuantity(order.Id);
        var orderRemaining = order.Quantity - orderShipped;
        if (orderRemaining <= 0) throw new InvalidOperationException("该销售订单已完成发运。");
        var receiptRemaining = ReceiptRemainingQuantity(receipt.Id);
        var quantity = decimal.Round(requestedQuantity ?? orderRemaining, 6, MidpointRounding.AwayFromZero);
        if (quantity <= 0) throw new InvalidOperationException("发运数量必须大于零。");
        if (quantity > orderRemaining) throw new InvalidOperationException("发运数量不能超过销售订单剩余数量。");
        if (quantity > receiptRemaining) throw new InvalidOperationException("完工入库可用数量不足，不能发运。");

        var item = new MomFinishedGoodsShipment(order.Id, receipt.Id, order.ProductId, receipt.WarehouseId, receipt.LocationId,
            quantity, NextSourceNo(order.OrderNo), shipmentDate, receipt.BatchNo, receipt.ExpiryDate, receipt.SerialNo, otherInfo);
        var completesOrder = orderShipped + item.Quantity >= order.Quantity;

        void Core()
        {
            inventoryService.Create(item.ProductId, item.WarehouseId, InventoryTransactionKind.Outbound, item.Quantity, item.SourceNo,
                item.ShipmentDate, $"销售订单 {order.OrderNo} 完工成品发运", item.LocationId, item.BatchNo, item.ExpiryDate, item.SerialNo);
            if (completesOrder) salesOrderShipmentService.ConfirmShipped(order);
            repository.Add(item);
        }

        if (transactions is null) Core();
        else transactions.Execute(Core, _ => salesOrderShipmentService.RestoreSubmittedAfterRollback(order));
        return item;
    }

    private SalesOrder GetSubmittedOrder(Guid salesOrderId)
    {
        var order = salesOrderRepository.List().FirstOrDefault(x => x.Id == salesOrderId)
            ?? throw new InvalidOperationException("销售订单不存在。");
        if (order.Status != SalesOrderStatus.Submitted) throw new InvalidOperationException("只有已提交的销售订单可以通过完工成品发运。");
        return order;
    }

    private decimal EffectiveShipmentQuantity(MomFinishedGoodsShipment shipment)
    {
        var allocations = allocationRepository.List(shipment.Id);
        return allocations.Count == 0 ? shipment.Quantity : allocations.Sum(x => x.Quantity);
    }

    private decimal ShippedFromReceipt(Guid receiptId)
    {
        var shipped = 0m;
        foreach (var shipment in repository.List())
        {
            var allocations = allocationRepository.List(shipment.Id);
            shipped += allocations.Count == 0
                ? shipment.FinishedGoodsReceiptId == receiptId ? shipment.Quantity : 0m
                : allocations.Where(x => x.FinishedGoodsReceiptId == receiptId).Sum(x => x.Quantity);
        }
        return shipped;
    }

    private string NextSourceNo(string orderNo)
    {
        var baseSourceNo = $"{orderNo}-OUT";
        for (var sequence = 1; ; sequence++)
        {
            var candidate = sequence == 1 ? baseSourceNo : $"{baseSourceNo}-P{sequence:D2}";
            if (!repository.List().Any(x => x.SourceNo.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                && !inventoryRepository.List().Any(x => x.SourceNo.Equals(candidate, StringComparison.OrdinalIgnoreCase))) return candidate;
        }
    }

    private string AllocationSourceNo(string shipmentSourceNo, int sequence)
    {
        var candidate = $"{shipmentSourceNo}-A{sequence:D2}";
        if (candidate.Length > 80) throw new InvalidOperationException("来源分配单号超过 80 个字符。");
        if (inventoryRepository.List().Any(x => x.SourceNo.Equals(candidate, StringComparison.OrdinalIgnoreCase))
            || allocationRepository.List().Any(x => x.SourceNo.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("来源分配单号已存在。");
        return candidate;
    }
}
