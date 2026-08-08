namespace VelrixWorkHub.Domain;

/// <summary>
/// 配送撤回不可变记录。它只撤回尚未消耗的配送数量，物理配送的库存反向调拨由 Application 负责。
/// </summary>
public sealed class MomMaterialDeliveryReversal
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid DeliveryId { get; private set; }
    public Guid RequirementId { get; private set; }
    public Guid WorkOrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid WorkCenterId { get; private set; }
    public decimal Quantity { get; private set; }
    public string SourceNo { get; private set; }
    public DateOnly OccurredOn { get; private set; }
    public string? Notes { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomMaterialDeliveryReversal(Guid deliveryId, Guid requirementId, Guid workOrderId, Guid productId,
        Guid workCenterId, decimal quantity, string sourceNo, DateOnly occurredOn, string? notes = null,
        string? otherInfo = null, Guid? id = null)
    {
        if (deliveryId == Guid.Empty) throw new ArgumentException("撤回必须绑定原配送记录。", nameof(deliveryId));
        if (requirementId == Guid.Empty) throw new ArgumentException("撤回必须绑定用料行。", nameof(requirementId));
        if (workOrderId == Guid.Empty) throw new ArgumentException("撤回必须绑定制造工单。", nameof(workOrderId));
        if (productId == Guid.Empty) throw new ArgumentException("撤回必须绑定商品。", nameof(productId));
        if (workCenterId == Guid.Empty) throw new ArgumentException("撤回必须绑定工作中心。", nameof(workCenterId));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "配送撤回数量必须大于零。");
        if (string.IsNullOrWhiteSpace(sourceNo)) throw new ArgumentException("配送撤回流水号不能为空。", nameof(sourceNo));
        Id = id ?? Guid.CreateVersion7(); DeliveryId = deliveryId; RequirementId = requirementId; WorkOrderId = workOrderId;
        ProductId = productId; WorkCenterId = workCenterId; Quantity = decimal.Round(quantity, 6, MidpointRounding.AwayFromZero);
        SourceNo = sourceNo.Trim(); OccurredOn = occurredOn; Notes = Clean(notes); OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public static string BuildSourceNo(Guid workOrderId, Guid reversalId) => $"MDR-{workOrderId:N}-{reversalId:N}";
    public static string BuildTransferNo(Guid workOrderId, Guid reversalId) => $"MOTR-{workOrderId:N}-{reversalId:N}";

    public static MomMaterialDeliveryReversal Restore(Guid id, Guid deliveryId, Guid requirementId, Guid workOrderId,
        Guid productId, Guid workCenterId, decimal quantity, string sourceNo, DateOnly occurredOn, string? notes, string? otherInfo)
        => new(deliveryId, requirementId, workOrderId, productId, workCenterId, quantity, sourceNo, occurredOn, notes, otherInfo, id);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
