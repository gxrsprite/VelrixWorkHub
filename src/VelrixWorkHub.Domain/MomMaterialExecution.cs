namespace VelrixWorkHub.Domain;

/// <summary>
/// 已领料物料配送到工单工作中心的执行记录。支持仅追踪配送和物理库位调拨两种来源。
/// </summary>
public sealed class MomMaterialDelivery
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid RequirementId { get; private set; }
    public Guid WorkOrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid WorkCenterId { get; private set; }
    public Guid? SourceWarehouseId { get; private set; }
    public Guid? SourceLocationId { get; private set; }
    public Guid? TargetWarehouseId { get; private set; }
    public Guid? TargetLocationId { get; private set; }
    public string? BatchNo { get; private set; }
    public DateOnly? ExpiryDate { get; private set; }
    public string? SerialNo { get; private set; }
    public decimal Quantity { get; private set; }
    public string SourceNo { get; private set; }
    public DateOnly OccurredOn { get; private set; }
    public string? Notes { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomMaterialDelivery(Guid requirementId, Guid workOrderId, Guid productId, Guid workCenterId,
        decimal quantity, string sourceNo, DateOnly occurredOn, string? notes = null, string? otherInfo = null, Guid? id = null,
        Guid? sourceWarehouseId = null, Guid? sourceLocationId = null, Guid? targetWarehouseId = null, Guid? targetLocationId = null,
        string? batchNo = null, DateOnly? expiryDate = null, string? serialNo = null)
    {
        if (requirementId == Guid.Empty) throw new ArgumentException("工位配送必须绑定用料行。", nameof(requirementId));
        if (workOrderId == Guid.Empty) throw new ArgumentException("工位配送必须绑定制造工单。", nameof(workOrderId));
        if (productId == Guid.Empty) throw new ArgumentException("工位配送必须绑定商品。", nameof(productId));
        if (workCenterId == Guid.Empty) throw new ArgumentException("工位配送必须绑定工作中心。", nameof(workCenterId));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "工位配送数量必须大于零。");
        if (string.IsNullOrWhiteSpace(sourceNo)) throw new ArgumentException("工位配送流水号不能为空。", nameof(sourceNo));
        if (sourceLocationId is not null && sourceWarehouseId is null) throw new ArgumentException("来源库位必须绑定来源仓库。", nameof(sourceLocationId));
        if (targetLocationId is not null && targetWarehouseId is null) throw new ArgumentException("目标库位必须绑定目标仓库。", nameof(targetLocationId));
        if ((sourceWarehouseId is null) != (targetWarehouseId is null)) throw new ArgumentException("物理配送必须同时绑定来源和目标仓库。", nameof(sourceWarehouseId));
        if (sourceWarehouseId is not null && sourceWarehouseId == targetWarehouseId && sourceLocationId == targetLocationId) throw new InvalidOperationException("物料来源和目标库位不能相同。");
        if (expiryDate is DateOnly expiry && expiry < occurredOn) throw new ArgumentException("保质期不能早于配送日期。", nameof(expiryDate));
        Id = id ?? Guid.CreateVersion7();
        RequirementId = requirementId; WorkOrderId = workOrderId; ProductId = productId; WorkCenterId = workCenterId;
        SourceWarehouseId = sourceWarehouseId; SourceLocationId = sourceLocationId; TargetWarehouseId = targetWarehouseId; TargetLocationId = targetLocationId;
        Quantity = decimal.Round(quantity, 6, MidpointRounding.AwayFromZero); SourceNo = sourceNo.Trim(); OccurredOn = occurredOn;
        Notes = Clean(notes); BatchNo = Clean(batchNo); ExpiryDate = expiryDate; SerialNo = Clean(serialNo); OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public static string BuildSourceNo(Guid workOrderId, Guid deliveryId) => $"MOD-{workOrderId:N}-{deliveryId:N}";
    public static string BuildTransferNo(Guid workOrderId, Guid deliveryId) => $"MOT-{workOrderId:N}-{deliveryId:N}";

    public static MomMaterialDelivery Restore(Guid id, Guid requirementId, Guid workOrderId, Guid productId,
        Guid workCenterId, decimal quantity, string sourceNo, DateOnly occurredOn, string? notes, string? otherInfo,
        Guid? sourceWarehouseId = null, Guid? sourceLocationId = null, Guid? targetWarehouseId = null, Guid? targetLocationId = null,
        string? batchNo = null, DateOnly? expiryDate = null, string? serialNo = null)
        => new(requirementId, workOrderId, productId, workCenterId, quantity, sourceNo, occurredOn, notes, otherInfo, id,
            sourceWarehouseId, sourceLocationId, targetWarehouseId, targetLocationId, batchNo, expiryDate, serialNo);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// 工位实际消耗记录。它消费已配送的 MOM 物料，不再次扣减 ERP 库存。
/// </summary>
public sealed class MomMaterialConsumption
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid RequirementId { get; private set; }
    /// <summary>精确消耗来源配送记录；历史记录可以为空，读取保持兼容。</summary>
    public Guid? DeliveryId { get; private set; }
    public Guid WorkOrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid WorkCenterId { get; private set; }
    public decimal Quantity { get; private set; }
    public string SourceNo { get; private set; }
    public DateOnly OccurredOn { get; private set; }
    public string? Notes { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomMaterialConsumption(Guid requirementId, Guid workOrderId, Guid productId, Guid workCenterId,
        decimal quantity, string sourceNo, DateOnly occurredOn, string? notes = null, string? otherInfo = null, Guid? id = null,
        Guid? deliveryId = null)
    {
        if (requirementId == Guid.Empty) throw new ArgumentException("实际消耗必须绑定用料行。", nameof(requirementId));
        if (workOrderId == Guid.Empty) throw new ArgumentException("实际消耗必须绑定制造工单。", nameof(workOrderId));
        if (productId == Guid.Empty) throw new ArgumentException("实际消耗必须绑定商品。", nameof(productId));
        if (workCenterId == Guid.Empty) throw new ArgumentException("实际消耗必须绑定工作中心。", nameof(workCenterId));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "实际消耗数量必须大于零。");
        if (string.IsNullOrWhiteSpace(sourceNo)) throw new ArgumentException("实际消耗流水号不能为空。", nameof(sourceNo));
        if (deliveryId == Guid.Empty) throw new ArgumentException("实际消耗配送来源无效。", nameof(deliveryId));
        Id = id ?? Guid.CreateVersion7();
        RequirementId = requirementId; DeliveryId = deliveryId; WorkOrderId = workOrderId; ProductId = productId; WorkCenterId = workCenterId;
        Quantity = decimal.Round(quantity, 6, MidpointRounding.AwayFromZero); SourceNo = sourceNo.Trim(); OccurredOn = occurredOn;
        Notes = Clean(notes); OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public static string BuildSourceNo(Guid workOrderId, Guid consumptionId) => $"MOC-{workOrderId:N}-{consumptionId:N}";

    public static MomMaterialConsumption Restore(Guid id, Guid requirementId, Guid workOrderId, Guid productId,
        Guid workCenterId, decimal quantity, string sourceNo, DateOnly occurredOn, string? notes, string? otherInfo, Guid? deliveryId = null)
        => new(requirementId, workOrderId, productId, workCenterId, quantity, sourceNo, occurredOn, notes, otherInfo, id, deliveryId);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
