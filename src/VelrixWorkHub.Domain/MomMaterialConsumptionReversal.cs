namespace VelrixWorkHub.Domain;

/// <summary>
/// 实际消耗逆向不可变记录。它只抵减原消耗的净余额，不修改原始消耗或历史分配。
/// </summary>
public sealed class MomMaterialConsumptionReversal
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ConsumptionId { get; private set; }
    public Guid? DeliveryId { get; private set; }
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

    public MomMaterialConsumptionReversal(Guid consumptionId, Guid? deliveryId, Guid requirementId, Guid workOrderId,
        Guid productId, Guid workCenterId, decimal quantity, string sourceNo, DateOnly occurredOn,
        string? batchNo = null, DateOnly? expiryDate = null, string? serialNo = null, string? notes = null,
        string? otherInfo = null, Guid? id = null)
    {
        if (consumptionId == Guid.Empty) throw new ArgumentException("消耗逆向必须绑定实际消耗记录。", nameof(consumptionId));
        if (deliveryId == Guid.Empty) throw new ArgumentException("消耗逆向配送来源无效。", nameof(deliveryId));
        if (requirementId == Guid.Empty) throw new ArgumentException("消耗逆向必须绑定用料行。", nameof(requirementId));
        if (workOrderId == Guid.Empty) throw new ArgumentException("消耗逆向必须绑定制造工单。", nameof(workOrderId));
        if (productId == Guid.Empty) throw new ArgumentException("消耗逆向必须绑定商品。", nameof(productId));
        if (workCenterId == Guid.Empty) throw new ArgumentException("消耗逆向必须绑定工作中心。", nameof(workCenterId));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "消耗逆向数量必须大于零。");
        if (string.IsNullOrWhiteSpace(sourceNo)) throw new ArgumentException("消耗逆向流水号不能为空。", nameof(sourceNo));
        Id = id ?? Guid.CreateVersion7(); ConsumptionId = consumptionId; DeliveryId = deliveryId; RequirementId = requirementId;
        WorkOrderId = workOrderId; ProductId = productId; WorkCenterId = workCenterId;
        Quantity = decimal.Round(quantity, 6, MidpointRounding.AwayFromZero); BatchNo = Clean(batchNo); ExpiryDate = expiryDate;
        SerialNo = Clean(serialNo); SourceNo = sourceNo.Trim(); OccurredOn = occurredOn; Notes = Clean(notes);
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public static string BuildSourceNo(Guid workOrderId, Guid reversalId) => $"MOCR-{workOrderId:N}-{reversalId:N}";

    public static MomMaterialConsumptionReversal Restore(Guid id, Guid consumptionId, Guid? deliveryId, Guid requirementId,
        Guid workOrderId, Guid productId, Guid workCenterId, decimal quantity, string sourceNo, DateOnly occurredOn,
        string? batchNo, DateOnly? expiryDate, string? serialNo, string? notes, string? otherInfo)
        => new(consumptionId, deliveryId, requirementId, workOrderId, productId, workCenterId, quantity, sourceNo, occurredOn,
            batchNo, expiryDate, serialNo, notes, otherInfo, id);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
