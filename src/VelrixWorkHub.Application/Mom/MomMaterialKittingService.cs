using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.Warehouses;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public sealed record MomMaterialKittingLine(
    Guid RequirementId,
    int LineNo,
    Guid ProductId,
    decimal RequiredQuantity,
    decimal IssuedQuantity,
    decimal ReturnedQuantity,
    decimal NetIssuedQuantity,
    decimal RemainingQuantity,
    decimal DeliveredQuantity,
    decimal RemainingToDeliver,
    decimal ConsumedQuantity,
    decimal RemainingToConsume,
    decimal AvailableQuantity,
    decimal ShortageQuantity,
    MomMaterialRequirementStatus Status);

public sealed record MomMaterialKittingSnapshot(
    Guid WorkOrderId,
    Guid WarehouseId,
    Guid? LocationId,
    bool IsReady,
    IReadOnlyList<MomMaterialKittingLine> Lines);

/// <summary>
/// MOM-05A 工单用料与齐套服务。
/// 用料需求是已发布 BOM 的执行快照；库存写入必须经过 InventoryService，MOM 只记录动作投影。
/// </summary>
public sealed class MomMaterialKittingService(
    IMomWorkOrderRepository workOrderRepository,
    IMomWorkOrderMaterialRequirementRepository requirementRepository,
    IMomMaterialMovementRepository movementRepository,
    IMomMaterialDeliveryRepository deliveryRepository,
    IMomMaterialConsumptionRepository consumptionRepository,
    IMomMaterialDeliveryReversalRepository deliveryReversalRepository,
    IMomMaterialConsumptionAllocationRepository consumptionAllocationRepository,
    IMomMaterialConsumptionReversalRepository consumptionReversalRepository,
    IMomManufacturingVersionRepository manufacturingVersionRepository,
    IMomManufacturingComponentRepository manufacturingComponentRepository,
    IMomWorkCenterRepository workCenterRepository,
    IProductRepository productRepository,
    IWarehouseRepository warehouseRepository,
    InventoryService inventoryService,
    IWorkflowTransactionBoundary? transactions = null)
{
    public IReadOnlyList<MomWorkOrderMaterialRequirement> ListRequirements(Guid workOrderId)
        => requirementRepository.List().Where(x => x.WorkOrderId == workOrderId).OrderBy(x => x.LineNo).ToArray();

    public IReadOnlyList<MomMaterialMovement> ListMovements(Guid workOrderId)
        => movementRepository.List().Where(x => x.WorkOrderId == workOrderId).OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.SourceNo).ToArray();

    public IReadOnlyList<MomMaterialDelivery> ListDeliveries(Guid workOrderId)
        => deliveryRepository.List().Where(x => x.WorkOrderId == workOrderId).OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.SourceNo).ToArray();

    public IReadOnlyList<MomMaterialConsumption> ListConsumptions(Guid workOrderId)
        => consumptionRepository.List().Where(x => x.WorkOrderId == workOrderId).OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.SourceNo).ToArray();

    public IReadOnlyList<MomMaterialDeliveryReversal> ListDeliveryReversals(Guid workOrderId)
        => deliveryReversalRepository.List().Where(x => x.WorkOrderId == workOrderId).OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.SourceNo).ToArray();

    public IReadOnlyList<MomMaterialConsumptionAllocation> ListConsumptionAllocations(Guid workOrderId)
        => consumptionAllocationRepository.List().Where(x => x.WorkOrderId == workOrderId).OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.SourceNo).ToArray();

    public IReadOnlyList<MomMaterialConsumptionReversal> ListConsumptionReversals(Guid workOrderId)
        => consumptionReversalRepository.List().Where(x => x.WorkOrderId == workOrderId).OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.SourceNo).ToArray();

    public IReadOnlyList<MomWorkOrderMaterialRequirement> EnsureRequirements(Guid workOrderId)
    {
        var workOrder = FindWorkOrder(workOrderId);
        EnsureRequirementWorkOrder(workOrder);
        var existing = ListRequirements(workOrderId);
        if (existing.Count > 0) return existing;

        var version = FindReleasedVersion(workOrder.ProductId, workOrder.PlannedStart)
            ?? throw new InvalidOperationException("工单计划日期没有有效的已发布制造版本。");
        var components = manufacturingComponentRepository.List()
            .Where(x => x.ManufacturingVersionId == version.Id)
            .OrderBy(x => x.LineNo)
            .ToArray();
        if (components.Length == 0) throw new InvalidOperationException("工单对应的制造版本没有 BOM 组件。");
        foreach (var component in components) EnsureActiveProduct(component.ComponentProductId);

        var requirements = components
            .Select(component => new MomWorkOrderMaterialRequirement(
                workOrder.Id,
                version.Id,
                component.LineNo,
                component.ComponentProductId,
                Round(workOrder.PlannedQuantity * component.QuantityPer * (1 + component.ScrapRatePercent / 100m))))
            .ToArray();

        void Persist()
        {
            foreach (var requirement in requirements) requirementRepository.Add(requirement);
        }

        if (transactions is null) Persist();
        else transactions.Execute(Persist);
        return requirements;
    }

    public MomMaterialKittingSnapshot CheckKitting(Guid workOrderId, Guid warehouseId, Guid? locationId = null)
    {
        EnsureWarehouse(warehouseId, locationId);
        var requirements = EnsureRequirements(workOrderId);
        var lines = requirements.Select(requirement =>
        {
            var delivered = DeliveredQuantity(requirement.Id);
            var consumed = ConsumedQuantity(requirement.Id);
            var available = AvailableQuantity(requirement.ComponentProductId, warehouseId, locationId, null, null, null);
            var shortage = Math.Max(0, requirement.RemainingQuantity - available);
            return new MomMaterialKittingLine(
                requirement.Id,
                requirement.LineNo,
                requirement.ComponentProductId,
                requirement.RequiredQuantity,
                requirement.IssuedQuantity,
                requirement.ReturnedQuantity,
                requirement.NetIssuedQuantity,
                requirement.RemainingQuantity,
                delivered,
                Math.Max(0, requirement.NetIssuedQuantity - delivered),
                consumed,
                Math.Max(0, delivered - consumed),
                Round(available),
                Round(shortage),
                requirement.Status);
        }).ToArray();
        return new MomMaterialKittingSnapshot(workOrderId, warehouseId, locationId, lines.All(x => x.ShortageQuantity <= 0), lines);
    }

    public MomMaterialMovement Issue(Guid requirementId, Guid warehouseId, Guid? locationId, decimal quantity,
        DateOnly occurredOn, string? notes = null, string? batchNo = null, DateOnly? expiryDate = null, string? serialNo = null)
    {
        var requirement = FindRequirement(requirementId);
        var workOrder = FindWorkOrder(requirement.WorkOrderId);
        EnsureIssueWorkOrder(workOrder);
        EnsureWarehouse(warehouseId, locationId);
        EnsureActiveProduct(requirement.ComponentProductId);
        EnsurePositiveQuantity(quantity, "领料数量必须大于零。");
        if (quantity > requirement.RemainingQuantity) throw new InvalidOperationException("领料数量不能超过工单用料剩余需求。");
        var available = AvailableQuantity(requirement.ComponentProductId, warehouseId, locationId, batchNo, expiryDate, serialNo);
        if (available < quantity) throw new InvalidOperationException($"库存不足，当前可用库存为 {available:N6}。");

        var movementId = Guid.CreateVersion7();
        var sourceNo = MomMaterialMovement.BuildSourceNo(workOrder.Id, MomMaterialMovementKind.Issue, movementId);
        var movement = new MomMaterialMovement(requirement.Id, workOrder.Id, requirement.ComponentProductId, warehouseId,
            MomMaterialMovementKind.Issue, quantity, sourceNo, occurredOn, locationId, notes ?? $"MOM 工单 {workOrder.WorkOrderNo} 领料",
            batchNo, expiryDate, serialNo, id: movementId);
        var originalIssued = requirement.IssuedQuantity;

        void Persist()
        {
            inventoryService.Create(requirement.ComponentProductId, warehouseId, InventoryTransactionKind.Outbound, quantity,
                sourceNo, occurredOn, movement.Notes, locationId, batchNo, expiryDate, serialNo);
            requirement.Issue(quantity);
            requirementRepository.Update(requirement);
            movementRepository.Add(movement);
        }

        if (transactions is null) Persist();
        else transactions.Execute(Persist, _ => requirement.RestoreMovementTotals(originalIssued, requirement.ReturnedQuantity));
        return movement;
    }

    public MomMaterialMovement Return(Guid requirementId, Guid warehouseId, Guid? locationId, decimal quantity,
        DateOnly occurredOn, string? notes = null, string? batchNo = null, DateOnly? expiryDate = null, string? serialNo = null)
    {
        var requirement = FindRequirement(requirementId);
        var workOrder = FindWorkOrder(requirement.WorkOrderId);
        EnsureIssueWorkOrder(workOrder);
        EnsureWarehouse(warehouseId, locationId);
        EnsureActiveProduct(requirement.ComponentProductId);
        EnsurePositiveQuantity(quantity, "退料数量必须大于零。");
        if (quantity > requirement.NetIssuedQuantity) throw new InvalidOperationException("退料数量不能超过工单用料净领料量。");
        var delivered = DeliveredQuantity(requirement.Id);
        if (quantity > Math.Max(0, requirement.NetIssuedQuantity - delivered))
            throw new InvalidOperationException("退料数量不能超过未配送的净领料量。");

        var movementId = Guid.CreateVersion7();
        var sourceNo = MomMaterialMovement.BuildSourceNo(workOrder.Id, MomMaterialMovementKind.Return, movementId);
        var movement = new MomMaterialMovement(requirement.Id, workOrder.Id, requirement.ComponentProductId, warehouseId,
            MomMaterialMovementKind.Return, quantity, sourceNo, occurredOn, locationId, notes ?? $"MOM 工单 {workOrder.WorkOrderNo} 退料",
            batchNo, expiryDate, serialNo, id: movementId);
        var originalReturned = requirement.ReturnedQuantity;

        void Persist()
        {
            inventoryService.Create(requirement.ComponentProductId, warehouseId, InventoryTransactionKind.Inbound, quantity,
                sourceNo, occurredOn, movement.Notes, locationId, batchNo, expiryDate, serialNo);
            requirement.Return(quantity);
            requirementRepository.Update(requirement);
            movementRepository.Add(movement);
        }

        if (transactions is null) Persist();
        else transactions.Execute(Persist, _ => requirement.RestoreMovementTotals(requirement.IssuedQuantity, originalReturned));
        return movement;
    }

    public MomMaterialDelivery Deliver(Guid requirementId, Guid workCenterId, decimal quantity, DateOnly occurredOn,
        string? notes = null, string? otherInfo = null, string? batchNo = null, DateOnly? expiryDate = null, string? serialNo = null)
    {
        var requirement = FindRequirement(requirementId);
        var workOrder = FindWorkOrder(requirement.WorkOrderId);
        EnsureExecutionWorkOrder(workOrder);
        EnsureWorkCenter(workOrder, workCenterId);
        EnsurePositiveQuantity(quantity, "工位配送数量必须大于零。");
        var remaining = Math.Max(0, requirement.NetIssuedQuantity - DeliveredQuantity(requirement.Id));
        if (quantity > remaining) throw new InvalidOperationException("工位配送数量不能超过未配送的净领料量。");

        var deliveryId = Guid.CreateVersion7();
        var delivery = new MomMaterialDelivery(requirement.Id, workOrder.Id, requirement.ComponentProductId, workCenterId,
            quantity, MomMaterialDelivery.BuildSourceNo(workOrder.Id, deliveryId), occurredOn,
            notes ?? $"MOM 工单 {workOrder.WorkOrderNo} 工位配送", otherInfo, deliveryId,
            batchNo: batchNo, expiryDate: expiryDate, serialNo: serialNo);
        void Persist() => deliveryRepository.Add(delivery);
        if (transactions is null) Persist(); else transactions.Execute(Persist);
        return delivery;
    }

    /// <summary>
    /// 将尚未领出的用料直接从来源仓库/库位调拨到工位线边目标库位。库存两笔流水由 InventoryService.Transfer 原子生成。
    /// </summary>
    public MomMaterialDelivery DeliverPhysically(Guid requirementId, Guid workCenterId,
        Guid sourceWarehouseId, Guid? sourceLocationId, Guid targetWarehouseId, Guid? targetLocationId,
        decimal quantity, DateOnly occurredOn, string? notes = null, string? batchNo = null,
        DateOnly? expiryDate = null, string? serialNo = null, string? otherInfo = null)
    {
        var requirement = FindRequirement(requirementId);
        var workOrder = FindWorkOrder(requirement.WorkOrderId);
        EnsureExecutionWorkOrder(workOrder);
        EnsureWorkCenter(workOrder, workCenterId);
        EnsureWarehouse(sourceWarehouseId, sourceLocationId);
        EnsureWarehouse(targetWarehouseId, targetLocationId);
        if (targetLocationId is null) throw new InvalidOperationException("物理配送必须选择目标库位。");
        if (sourceWarehouseId == targetWarehouseId && sourceLocationId == targetLocationId) throw new InvalidOperationException("物料来源和目标库位不能相同。");
        EnsureActiveProduct(requirement.ComponentProductId);
        EnsurePositiveQuantity(quantity, "工位配送数量必须大于零。");
        if (quantity > requirement.RemainingQuantity) throw new InvalidOperationException("物理配送数量不能超过工单用料剩余需求。");
        var available = AvailableQuantity(requirement.ComponentProductId, sourceWarehouseId, sourceLocationId, batchNo, expiryDate, serialNo);
        if (available < quantity) throw new InvalidOperationException($"来源库存不足，当前可用库存为 {available:N6}。");

        var deliveryId = Guid.CreateVersion7();
        var sourceNo = MomMaterialMovement.BuildSourceNo(workOrder.Id, MomMaterialMovementKind.Issue, deliveryId);
        var transferNo = MomMaterialDelivery.BuildTransferNo(workOrder.Id, deliveryId);
        var delivery = new MomMaterialDelivery(requirement.Id, workOrder.Id, requirement.ComponentProductId, workCenterId,
            quantity, MomMaterialDelivery.BuildSourceNo(workOrder.Id, deliveryId), occurredOn,
            notes ?? $"MOM 工单 {workOrder.WorkOrderNo} 物理配送", otherInfo, deliveryId,
            sourceWarehouseId, sourceLocationId, targetWarehouseId, targetLocationId, batchNo, expiryDate, serialNo);
        var movement = new MomMaterialMovement(requirement.Id, workOrder.Id, requirement.ComponentProductId, sourceWarehouseId,
            MomMaterialMovementKind.Issue, quantity, sourceNo, occurredOn, sourceLocationId,
            delivery.Notes, batchNo, expiryDate, serialNo, id: deliveryId);
        var originalIssued = requirement.IssuedQuantity;

        void Persist()
        {
            inventoryService.Transfer(requirement.ComponentProductId, sourceWarehouseId, sourceLocationId,
                targetWarehouseId, targetLocationId, quantity, transferNo, occurredOn, batchNo, expiryDate, serialNo);
            requirement.Issue(quantity);
            requirementRepository.Update(requirement);
            movementRepository.Add(movement);
            deliveryRepository.Add(delivery);
        }

        if (transactions is null) Persist();
        else transactions.Execute(Persist, _ => requirement.RestoreMovementTotals(originalIssued, requirement.ReturnedQuantity));
        return delivery;
    }

    /// <summary>
    /// 撤回尚未消耗的配送。逻辑配送只回写 MOM 执行账，物理配送额外通过库存 Application 反向调拨。
    /// </summary>
    public MomMaterialDeliveryReversal WithdrawDelivery(Guid deliveryId, decimal quantity, DateOnly occurredOn,
        string? notes = null, string? otherInfo = null)
    {
        var delivery = deliveryRepository.List().FirstOrDefault(x => x.Id == deliveryId)
            ?? throw new InvalidOperationException("配送记录不存在。");
        var requirement = FindRequirement(delivery.RequirementId);
        var workOrder = FindWorkOrder(delivery.WorkOrderId);
        EnsureExecutionWorkOrder(workOrder);
        EnsureWorkCenter(workOrder, delivery.WorkCenterId);
        EnsurePositiveQuantity(quantity, "配送撤回数量必须大于零。");

        var remainingOnDelivery = Math.Max(0, delivery.Quantity - DeliveryReversedQuantity(delivery.Id));
        if (quantity > remainingOnDelivery) throw new InvalidOperationException("配送撤回数量不能超过该配送未撤回数量。");
        if (quantity > Math.Max(0, DeliveredQuantity(requirement.Id) - ConsumedQuantity(requirement.Id)))
            throw new InvalidOperationException("已消耗物料不能撤回。");

        var reversalId = Guid.CreateVersion7();
        var sourceNo = MomMaterialDeliveryReversal.BuildSourceNo(workOrder.Id, reversalId);
        var reversal = new MomMaterialDeliveryReversal(delivery.Id, requirement.Id, workOrder.Id, requirement.ComponentProductId,
            delivery.WorkCenterId, quantity, sourceNo, occurredOn,
            notes ?? $"MOM 工单 {workOrder.WorkOrderNo} 撤回工位配送", otherInfo, reversalId);
        MomMaterialMovement? movement = null;
        var originalReturned = requirement.ReturnedQuantity;

        void Persist()
        {
            if (delivery.SourceWarehouseId is not null || delivery.TargetWarehouseId is not null || delivery.SourceLocationId is not null || delivery.TargetLocationId is not null)
            {
                if (delivery.SourceWarehouseId is not Guid sourceWarehouseId || delivery.TargetWarehouseId is not Guid targetWarehouseId)
                    throw new InvalidOperationException("物理配送端点不完整，不能撤回。");
                EnsureWarehouse(sourceWarehouseId, delivery.SourceLocationId);
                EnsureWarehouse(targetWarehouseId, delivery.TargetLocationId);
                inventoryService.Transfer(requirement.ComponentProductId, targetWarehouseId, delivery.TargetLocationId,
                    sourceWarehouseId, delivery.SourceLocationId, quantity, MomMaterialDeliveryReversal.BuildTransferNo(workOrder.Id, reversalId),
                    occurredOn, delivery.BatchNo, delivery.ExpiryDate, delivery.SerialNo);
                movement = new MomMaterialMovement(requirement.Id, workOrder.Id, requirement.ComponentProductId, targetWarehouseId,
                    MomMaterialMovementKind.Return, quantity,
                    MomMaterialMovement.BuildSourceNo(workOrder.Id, MomMaterialMovementKind.Return, reversalId), occurredOn, delivery.TargetLocationId,
                    reversal.Notes, delivery.BatchNo, delivery.ExpiryDate, delivery.SerialNo, id: reversalId);
                movementRepository.Add(movement);
            }
            requirement.Return(quantity);
            requirementRepository.Update(requirement);
            deliveryReversalRepository.Add(reversal);
        }

        if (transactions is null) Persist();
        else transactions.Execute(Persist, _ => requirement.RestoreMovementTotals(requirement.IssuedQuantity, originalReturned));
        return reversal;
    }

    public MomMaterialConsumption Consume(Guid requirementId, Guid workCenterId, decimal quantity, DateOnly occurredOn, string? notes = null, string? otherInfo = null, Guid? deliveryId = null)
    {
        var requirement = FindRequirement(requirementId);
        var workOrder = FindWorkOrder(requirement.WorkOrderId);
        if (workOrder.Status != MomWorkOrderStatus.InProgress)
            throw new InvalidOperationException("只有执行中的工单可以登记实际消耗。");
        EnsureWorkCenter(workOrder, workCenterId);
        EnsurePositiveQuantity(quantity, "实际消耗数量必须大于零。");
        var remaining = Math.Max(0, DeliveredQuantity(requirement.Id) - ConsumedQuantity(requirement.Id));
        if (quantity > remaining) throw new InvalidOperationException("实际消耗数量不能超过已配送未消耗量。");

        MomMaterialDelivery? delivery = null;
        if (deliveryId is Guid selectedDeliveryId)
        {
            delivery = deliveryRepository.List().FirstOrDefault(x => x.Id == selectedDeliveryId)
                ?? throw new InvalidOperationException("消耗配送来源不存在。");
            if (delivery.RequirementId != requirement.Id || delivery.WorkOrderId != workOrder.Id || delivery.ProductId != requirement.ComponentProductId)
                throw new InvalidOperationException("消耗配送来源与用料行不一致。");
            if (delivery.WorkCenterId != workCenterId)
                throw new InvalidOperationException("消耗配送来源与工作中心不一致。");
            var deliveryRemaining = Math.Max(0, delivery.Quantity - DeliveryReversedQuantity(delivery.Id) - ConsumedQuantityForDelivery(delivery.Id));
            if (quantity > deliveryRemaining)
                throw new InvalidOperationException("实际消耗数量不能超过所选配送记录的未消耗数量。");
        }

        var allocations = delivery is null
            ? Array.Empty<(MomMaterialDelivery Delivery, decimal Quantity)>()
            : new[] { (Delivery: delivery, Quantity: quantity) };
        return RegisterConsumption(requirement, workOrder, workCenterId, quantity, occurredOn, notes, otherInfo, allocations, delivery?.Id);
    }

    /// <summary>
    /// 按指定批次在多条工位配送记录之间自动分配实际消耗。分配顺序固定为配送日期、流水号，避免同一批次的剩余量漂移。
    /// </summary>
    public MomMaterialConsumption ConsumeByBatch(Guid requirementId, Guid workCenterId, string batchNo, decimal quantity,
        DateOnly occurredOn, string? notes = null, string? otherInfo = null)
    {
        var requirement = FindRequirement(requirementId);
        var workOrder = FindWorkOrder(requirement.WorkOrderId);
        if (workOrder.Status != MomWorkOrderStatus.InProgress)
            throw new InvalidOperationException("只有执行中的工单可以登记实际消耗。");
        EnsureWorkCenter(workOrder, workCenterId);
        EnsurePositiveQuantity(quantity, "实际消耗数量必须大于零。");
        var normalizedBatch = batchNo?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedBatch)) throw new InvalidOperationException("批次消耗必须选择批次号。");

        var candidates = deliveryRepository.List()
            .Where(x => x.RequirementId == requirement.Id && x.WorkOrderId == workOrder.Id && x.ProductId == requirement.ComponentProductId
                && x.WorkCenterId == workCenterId && string.Equals(x.BatchNo, normalizedBatch, StringComparison.OrdinalIgnoreCase))
            .Select(x => (Delivery: x, Remaining: DeliveryRemainingQuantity(x)))
            .Where(x => x.Remaining > 0)
            .OrderBy(x => x.Delivery.OccurredOn)
            .ThenBy(x => x.Delivery.SourceNo)
            .ToArray();
        var available = candidates.Sum(x => x.Remaining);
        if (quantity > available)
            throw new InvalidOperationException("批次实际消耗数量不能超过该批次配送剩余量。");

        var allocations = new List<(MomMaterialDelivery Delivery, decimal Quantity)>();
        var remaining = quantity;
        foreach (var candidate in candidates)
        {
            if (remaining <= 0) break;
            var allocated = Math.Min(remaining, candidate.Remaining);
            allocations.Add((candidate.Delivery, allocated));
            remaining -= allocated;
        }
        return RegisterConsumption(requirement, workOrder, workCenterId, quantity, occurredOn, notes, otherInfo,
            allocations, allocations.Count == 1 ? allocations[0].Delivery.Id : null);
    }

    /// <summary>
    /// 逆向已登记消耗。原消耗和分配保持不变，逆向按原配送分配顺序拆成不可变记录并恢复配送可消耗余额。
    /// </summary>
    public IReadOnlyList<MomMaterialConsumptionReversal> ReverseConsumption(Guid consumptionId, decimal quantity,
        DateOnly occurredOn, string? notes = null, string? otherInfo = null)
    {
        var consumption = consumptionRepository.List().FirstOrDefault(x => x.Id == consumptionId)
            ?? throw new InvalidOperationException("实际消耗记录不存在。");
        var workOrder = FindWorkOrder(consumption.WorkOrderId);
        if (workOrder.Status != MomWorkOrderStatus.InProgress)
            throw new InvalidOperationException("只有执行中的工单可以逆向实际消耗。");
        EnsureWorkCenter(workOrder, consumption.WorkCenterId);
        EnsurePositiveQuantity(quantity, "消耗逆向数量必须大于零。");

        var reversedTotal = consumptionReversalRepository.List().Where(x => x.ConsumptionId == consumption.Id).Sum(x => x.Quantity);
        var remaining = Math.Max(0, consumption.Quantity - reversedTotal);
        if (quantity > remaining) throw new InvalidOperationException("消耗逆向数量不能超过该消耗未逆向数量。");

        var sources = new List<(Guid? DeliveryId, decimal Available, string? BatchNo, DateOnly? ExpiryDate, string? SerialNo)>();
        var deliveryById = deliveryRepository.List().ToDictionary(x => x.Id);
        var allocations = consumptionAllocationRepository.List().Where(x => x.ConsumptionId == consumption.Id)
            .OrderBy(x => deliveryById.TryGetValue(x.DeliveryId, out var delivery) ? delivery.OccurredOn : x.OccurredOn)
            .ThenBy(x => deliveryById.TryGetValue(x.DeliveryId, out var delivery) ? delivery.SourceNo : x.SourceNo)
            .ToArray();
        if (allocations.Length > 0)
        {
            foreach (var allocation in allocations)
            {
                var sourceReversed = consumptionReversalRepository.List()
                    .Where(x => x.ConsumptionId == consumption.Id && x.DeliveryId == allocation.DeliveryId).Sum(x => x.Quantity);
                sources.Add((allocation.DeliveryId, Math.Max(0, allocation.Quantity - sourceReversed), allocation.BatchNo, allocation.ExpiryDate, allocation.SerialNo));
            }
        }
        else if (consumption.DeliveryId is Guid deliveryId)
        {
            var delivery = deliveryRepository.List().FirstOrDefault(x => x.Id == deliveryId)
                ?? throw new InvalidOperationException("实际消耗配送来源不存在。");
            var sourceReversed = consumptionReversalRepository.List()
                .Where(x => x.ConsumptionId == consumption.Id && x.DeliveryId == deliveryId).Sum(x => x.Quantity);
            sources.Add((deliveryId, Math.Max(0, consumption.Quantity - sourceReversed), delivery.BatchNo, delivery.ExpiryDate, delivery.SerialNo));
        }
        else
        {
            var unallocatedReversed = consumptionReversalRepository.List()
                .Where(x => x.ConsumptionId == consumption.Id && x.DeliveryId is null).Sum(x => x.Quantity);
            sources.Add((null, Math.Max(0, consumption.Quantity - unallocatedReversed), null, null, null));
        }

        if (sources.Sum(x => x.Available) < quantity)
            throw new InvalidOperationException("实际消耗分配记录不完整，不能安全逆向。");

        var reversals = new List<MomMaterialConsumptionReversal>();
        var left = quantity;
        foreach (var source in sources)
        {
            if (left <= 0) break;
            var reversalQuantity = Math.Min(left, source.Available);
            var reversalId = Guid.CreateVersion7();
            reversals.Add(new MomMaterialConsumptionReversal(consumption.Id, source.DeliveryId, consumption.RequirementId,
                consumption.WorkOrderId, consumption.ProductId, consumption.WorkCenterId, reversalQuantity,
                MomMaterialConsumptionReversal.BuildSourceNo(workOrder.Id, reversalId), occurredOn,
                source.BatchNo, source.ExpiryDate, source.SerialNo, notes ?? $"MOM 工单 {workOrder.WorkOrderNo} 逆向实际消耗", otherInfo, reversalId));
            left -= reversalQuantity;
        }

        void Persist()
        {
            foreach (var reversal in reversals) consumptionReversalRepository.Add(reversal);
        }
        if (transactions is null) Persist(); else transactions.Execute(Persist);
        return reversals;
    }

    private MomMaterialConsumption RegisterConsumption(MomWorkOrderMaterialRequirement requirement, MomWorkOrder workOrder,
        Guid workCenterId, decimal quantity, DateOnly occurredOn, string? notes, string? otherInfo,
        IReadOnlyList<(MomMaterialDelivery Delivery, decimal Quantity)> allocations, Guid? deliveryId)
    {
        var remaining = Math.Max(0, DeliveredQuantity(requirement.Id) - ConsumedQuantity(requirement.Id));
        if (quantity > remaining) throw new InvalidOperationException("实际消耗数量不能超过已配送未消耗量。");
        foreach (var allocation in allocations)
        {
            var currentRemaining = DeliveryRemainingQuantity(allocation.Delivery);
            if (allocation.Quantity > currentRemaining)
                throw new InvalidOperationException("实际消耗数量不能超过所选配送记录的未消耗数量。");
        }

        var consumptionId = Guid.CreateVersion7();
        var consumption = new MomMaterialConsumption(requirement.Id, workOrder.Id, requirement.ComponentProductId, workCenterId,
            quantity, MomMaterialConsumption.BuildSourceNo(workOrder.Id, consumptionId), occurredOn,
            notes ?? $"MOM 工单 {workOrder.WorkOrderNo} 实际消耗", otherInfo, consumptionId, deliveryId);
        var allocationRecords = allocations.Select(allocation =>
        {
            var allocationId = Guid.CreateVersion7();
            return new MomMaterialConsumptionAllocation(consumption.Id, allocation.Delivery.Id, requirement.Id, workOrder.Id,
                requirement.ComponentProductId, workCenterId, allocation.Quantity,
                MomMaterialConsumptionAllocation.BuildSourceNo(workOrder.Id, allocationId), occurredOn,
                allocation.Delivery.BatchNo, allocation.Delivery.ExpiryDate, allocation.Delivery.SerialNo, consumption.Notes, otherInfo, allocationId);
        }).ToArray();
        void Persist()
        {
            consumptionRepository.Add(consumption);
            foreach (var allocation in allocationRecords) consumptionAllocationRepository.Add(allocation);
        }
        if (transactions is null) Persist(); else transactions.Execute(Persist);
        return consumption;
    }

    private MomWorkOrder FindWorkOrder(Guid id) => workOrderRepository.List().FirstOrDefault(x => x.Id == id)
        ?? throw new InvalidOperationException("制造工单不存在。");

    private MomWorkOrderMaterialRequirement FindRequirement(Guid id) => requirementRepository.List().FirstOrDefault(x => x.Id == id)
        ?? throw new InvalidOperationException("工单用料不存在。");

    private void EnsureRequirementWorkOrder(MomWorkOrder workOrder)
    {
        if (workOrder.Status is not (MomWorkOrderStatus.Released or MomWorkOrderStatus.InProgress or MomWorkOrderStatus.Completed))
            throw new InvalidOperationException("只有已下达、执行中或已完工工单可以生成用料需求。");
    }

    private static void EnsureIssueWorkOrder(MomWorkOrder workOrder)
    {
        if (workOrder.Status is not (MomWorkOrderStatus.Released or MomWorkOrderStatus.InProgress))
            throw new InvalidOperationException("只有已下达或执行中的工单可以领料或退料。");
    }

    private static void EnsureExecutionWorkOrder(MomWorkOrder workOrder)
    {
        if (workOrder.Status is not (MomWorkOrderStatus.Released or MomWorkOrderStatus.InProgress))
            throw new InvalidOperationException("只有已下达或执行中的工单可以进行工位配送。");
    }

    private void EnsureWorkCenter(MomWorkOrder workOrder, Guid workCenterId)
    {
        if (workOrder.WorkCenterId is null) throw new InvalidOperationException("工单未绑定工作中心，不能执行工位物料动作。");
        if (workOrder.WorkCenterId != workCenterId) throw new InvalidOperationException("所选工作中心与工单不一致。");
        var workCenter = workCenterRepository.List().FirstOrDefault(x => x.Id == workCenterId)
            ?? throw new InvalidOperationException("工作中心不存在。");
        if (workCenter.Status != MomMasterDataStatus.Active) throw new InvalidOperationException("工作中心已停用，不能执行工位物料动作。");
    }

    private decimal DeliveredQuantity(Guid requirementId)
    {
        var delivered = deliveryRepository.List().Where(x => x.RequirementId == requirementId).Sum(x => x.Quantity);
        var reversed = deliveryReversalRepository.List().Where(x => x.RequirementId == requirementId).Sum(x => x.Quantity);
        return Math.Max(0, delivered - reversed);
    }

    private decimal DeliveryReversedQuantity(Guid deliveryId) => deliveryReversalRepository.List()
        .Where(x => x.DeliveryId == deliveryId).Sum(x => x.Quantity);

    private decimal ConsumedQuantity(Guid requirementId) => Math.Max(0,
        consumptionRepository.List().Where(x => x.RequirementId == requirementId).Sum(x => x.Quantity)
        - consumptionReversalRepository.List().Where(x => x.RequirementId == requirementId).Sum(x => x.Quantity));

    private decimal ConsumedQuantityForDelivery(Guid deliveryId) => Math.Max(0,
        consumptionRepository.List()
            .Where(x => x.DeliveryId == deliveryId && !consumptionAllocationRepository.List().Any(a => a.ConsumptionId == x.Id)).Sum(x => x.Quantity)
        + consumptionAllocationRepository.List().Where(x => x.DeliveryId == deliveryId).Sum(x => x.Quantity)
        - consumptionReversalRepository.List().Where(x => x.DeliveryId == deliveryId).Sum(x => x.Quantity));

    private decimal DeliveryRemainingQuantity(MomMaterialDelivery delivery)
        => Math.Max(0, delivery.Quantity - DeliveryReversedQuantity(delivery.Id) - ConsumedQuantityForDelivery(delivery.Id));

    private void EnsureActiveProduct(Guid productId)
    {
        var product = productRepository.List().FirstOrDefault(x => x.Id == productId)
            ?? throw new InvalidOperationException("用料商品不存在。");
        if (product.Status != ProductStatus.Active) throw new InvalidOperationException("停用商品不能用于工单用料。");
    }

    private Warehouse EnsureWarehouse(Guid warehouseId, Guid? locationId)
    {
        var warehouse = warehouseRepository.List().FirstOrDefault(x => x.Id == warehouseId)
            ?? throw new InvalidOperationException("仓库不存在。");
        if (warehouse.Status != WarehouseStatus.Active) throw new InvalidOperationException("仓库已停用，不能用于工单领退料。");
        if (locationId is not null && !warehouse.Locations.Any(x => x.Id == locationId)) throw new InvalidOperationException("库位不属于所选仓库。");
        return warehouse;
    }

    private decimal AvailableQuantity(Guid productId, Guid warehouseId, Guid? locationId, string? batchNo, DateOnly? expiryDate, string? serialNo)
    {
        var normalizedBatch = batchNo?.Trim();
        var normalizedSerial = serialNo?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSerial))
            return inventoryService.SerialBalances().Where(x => x.ProductId == productId && x.WarehouseId == warehouseId && x.LocationId == locationId && x.SerialNo.Equals(normalizedSerial, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Quantity);
        if (!string.IsNullOrWhiteSpace(normalizedBatch))
            return inventoryService.BatchBalances().Where(x => x.ProductId == productId && x.WarehouseId == warehouseId && x.LocationId == locationId && x.BatchNo.Equals(normalizedBatch, StringComparison.OrdinalIgnoreCase) && (expiryDate is null || x.ExpiryDate == expiryDate)).Sum(x => x.Quantity);
        return locationId is null
            ? inventoryService.Balances().FirstOrDefault(x => x.ProductId == productId && x.WarehouseId == warehouseId)?.Quantity ?? 0
            : inventoryService.LocationBalances().FirstOrDefault(x => x.ProductId == productId && x.WarehouseId == warehouseId && x.LocationId == locationId)?.Quantity ?? 0;
    }

    private MomManufacturingVersion? FindReleasedVersion(Guid productId, DateOnly date) => manufacturingVersionRepository.List()
        .Where(x => x.ProductId == productId && x.Status == MomManufacturingVersionStatus.Released && x.EffectiveFrom <= date && (x.EffectiveTo is null || x.EffectiveTo >= date))
        .OrderByDescending(x => x.EffectiveFrom)
        .FirstOrDefault();

    private static void EnsurePositiveQuantity(decimal quantity, string message)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), message);
    }

    private static decimal Round(decimal value) => decimal.Round(Math.Max(0, value), 6, MidpointRounding.AwayFromZero);
}
