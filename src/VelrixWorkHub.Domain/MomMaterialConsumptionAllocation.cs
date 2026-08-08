namespace VelrixWorkHub.Domain;

/// <summary>
/// 实际消耗与具体配送记录之间的不可变分配。一个消耗可以拆到同一批次的多条配送记录。
/// </summary>
public sealed class MomMaterialConsumptionAllocation
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ConsumptionId { get; private set; }
    public Guid DeliveryId { get; private set; }
    public Guid RequirementId { get; private set; }
    public Guid WorkOrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid WorkCenterId { get; private set; }
    public decimal Quantity { get; private set; }
    public string? BatchNo { get; private set; }
    public DateOnly? ExpiryDate { get; private set; }
    public string? SerialNo { get; private set; }
    public string SourceNo { get; private set; }
    public DateOnly OccurredOn { get; private set; }
    public string? Notes { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomMaterialConsumptionAllocation(Guid consumptionId, Guid deliveryId, Guid requirementId, Guid workOrderId,
        Guid productId, Guid workCenterId, decimal quantity, string sourceNo, DateOnly occurredOn,
        string? batchNo = null, DateOnly? expiryDate = null, string? serialNo = null, string? notes = null,
        string? otherInfo = null, Guid? id = null)
    {
        if (consumptionId == Guid.Empty) throw new ArgumentException("消耗分配必须绑定实际消耗记录。", nameof(consumptionId));
        if (deliveryId == Guid.Empty) throw new ArgumentException("消耗分配必须绑定配送记录。", nameof(deliveryId));
        if (requirementId == Guid.Empty) throw new ArgumentException("消耗分配必须绑定用料行。", nameof(requirementId));
        if (workOrderId == Guid.Empty) throw new ArgumentException("消耗分配必须绑定制造工单。", nameof(workOrderId));
        if (productId == Guid.Empty) throw new ArgumentException("消耗分配必须绑定商品。", nameof(productId));
        if (workCenterId == Guid.Empty) throw new ArgumentException("消耗分配必须绑定工作中心。", nameof(workCenterId));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "消耗分配数量必须大于零。");
        if (string.IsNullOrWhiteSpace(sourceNo)) throw new ArgumentException("消耗分配流水号不能为空。", nameof(sourceNo));
        Id = id ?? Guid.CreateVersion7();
        ConsumptionId = consumptionId; DeliveryId = deliveryId; RequirementId = requirementId; WorkOrderId = workOrderId;
        ProductId = productId; WorkCenterId = workCenterId; Quantity = decimal.Round(quantity, 6, MidpointRounding.AwayFromZero);
        BatchNo = Clean(batchNo); ExpiryDate = expiryDate; SerialNo = Clean(serialNo); SourceNo = sourceNo.Trim(); OccurredOn = occurredOn;
        Notes = Clean(notes); OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public static string BuildSourceNo(Guid workOrderId, Guid allocationId) => $"MOCA-{workOrderId:N}-{allocationId:N}";

    public static MomMaterialConsumptionAllocation Restore(Guid id, Guid consumptionId, Guid deliveryId, Guid requirementId,
        Guid workOrderId, Guid productId, Guid workCenterId, decimal quantity, string sourceNo, DateOnly occurredOn,
        string? batchNo, DateOnly? expiryDate, string? serialNo, string? notes, string? otherInfo)
        => new(consumptionId, deliveryId, requirementId, workOrderId, productId, workCenterId, quantity, sourceNo, occurredOn,
            batchNo, expiryDate, serialNo, notes, otherInfo, id);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
