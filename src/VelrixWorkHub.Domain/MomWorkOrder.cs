namespace VelrixWorkHub.Domain;

public enum MomWorkOrderStatus { Draft, Planned, Released, InProgress, Completed, Cancelled }
public enum MomWorkOrderSourceKind { Manual, SalesOrder, PmsProject, Planning }

/// <summary>
/// MOM 首版制造工单。它是计划、领料、工序报工、质量和完工入库后续过程的稳定根单。
/// </summary>
public sealed class MomWorkOrder
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string WorkOrderNo { get; private set; } = string.Empty;
    public Guid ProductId { get; private set; }
    public Guid? WorkCenterId { get; private set; }
    public Guid? SalesOrderId { get; private set; }
    public Guid? PmsProjectId { get; private set; }
    public DateOnly PlannedStart { get; private set; }
    public DateOnly PlannedEnd { get; private set; }
    public decimal PlannedQuantity { get; private set; }
    public decimal CompletedQuantity { get; private set; }
    public decimal RemainingQuantity => Math.Max(0, PlannedQuantity - CompletedQuantity);
    public MomWorkOrderStatus Status { get; private set; }
    public MomWorkOrderSourceKind SourceKind { get; private set; }
    public string? SourceDocumentNo { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomWorkOrder(string workOrderNo, Guid productId, DateOnly plannedStart, DateOnly plannedEnd,
        decimal plannedQuantity, MomWorkOrderSourceKind sourceKind = MomWorkOrderSourceKind.Manual,
        string? sourceDocumentNo = null, Guid? salesOrderId = null, Guid? pmsProjectId = null, string? otherInfo = null)
    {
        Validate(workOrderNo, productId, plannedStart, plannedEnd, plannedQuantity, sourceKind, sourceDocumentNo, salesOrderId, pmsProjectId);
        WorkOrderNo = workOrderNo.Trim(); ProductId = productId; PlannedStart = plannedStart; PlannedEnd = plannedEnd;
        PlannedQuantity = plannedQuantity; SourceKind = sourceKind; SourceDocumentNo = Clean(sourceDocumentNo);
        SalesOrderId = salesOrderId; PmsProjectId = pmsProjectId; OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
        Status = MomWorkOrderStatus.Draft;
    }

    public static MomWorkOrder Restore(Guid id, string workOrderNo, Guid productId, DateOnly plannedStart, DateOnly plannedEnd,
        decimal plannedQuantity, decimal completedQuantity, MomWorkOrderStatus status, MomWorkOrderSourceKind sourceKind,
        string? sourceDocumentNo, Guid? salesOrderId, Guid? pmsProjectId, Guid? workCenterId, string? otherInfo)
    {
        var item = new MomWorkOrder(workOrderNo, productId, plannedStart, plannedEnd, plannedQuantity, sourceKind, sourceDocumentNo, salesOrderId, pmsProjectId, otherInfo) { WorkCenterId = workCenterId };
        if (completedQuantity < 0 || completedQuantity > plannedQuantity) throw new InvalidOperationException("制造工单完工数量超出计划数量。");
        item.Id = id; item.CompletedQuantity = completedQuantity; item.Status = status; return item;
    }

    public void SetStatus(MomWorkOrderStatus status)
    {
        if (status == Status) return;
        var allowed = (Status, status) switch
        {
            (MomWorkOrderStatus.Draft, MomWorkOrderStatus.Planned) => true,
            (MomWorkOrderStatus.Draft, MomWorkOrderStatus.Cancelled) => true,
            (MomWorkOrderStatus.Planned, MomWorkOrderStatus.Released) => WorkCenterId is not null,
            (MomWorkOrderStatus.Planned, MomWorkOrderStatus.Cancelled) => true,
            (MomWorkOrderStatus.Released, MomWorkOrderStatus.InProgress) => true,
            (MomWorkOrderStatus.Released, MomWorkOrderStatus.Cancelled) => true,
            (MomWorkOrderStatus.InProgress, MomWorkOrderStatus.Completed) => CompletedQuantity >= PlannedQuantity,
            (MomWorkOrderStatus.InProgress, MomWorkOrderStatus.Cancelled) => true,
            _ => false
        };
        if (!allowed) throw new InvalidOperationException(Status == MomWorkOrderStatus.InProgress && status == MomWorkOrderStatus.Completed
            ? "制造工单完工数量未达到计划数量。" : Status == MomWorkOrderStatus.Planned && status == MomWorkOrderStatus.Released && WorkCenterId is null
                ? "工单下达前必须绑定启用的工作中心。" : $"制造工单不能从“{Status}”变更为“{status}”。");
        Status = status;
    }

    public void SetWorkCenter(Guid workCenterId)
    {
        if (workCenterId == Guid.Empty) throw new ArgumentException("工作中心不能为空。", nameof(workCenterId));
        if (Status is not (MomWorkOrderStatus.Draft or MomWorkOrderStatus.Planned)) throw new InvalidOperationException("只有草稿或已计划工单可以绑定工作中心。");
        WorkCenterId = workCenterId;
    }

    public void SetCompletedQuantity(decimal quantity)
    {
        if (Status is not (MomWorkOrderStatus.Released or MomWorkOrderStatus.InProgress)) throw new InvalidOperationException("只有已下达或执行中的制造工单可以登记完工数量。");
        if (quantity < 0 || quantity > PlannedQuantity) throw new ArgumentOutOfRangeException(nameof(quantity), "完工数量必须在 0 到计划数量之间。");
        CompletedQuantity = quantity;
        if (quantity > 0 && Status == MomWorkOrderStatus.Released) Status = MomWorkOrderStatus.InProgress;
    }

    private static void Validate(string workOrderNo, Guid productId, DateOnly plannedStart, DateOnly plannedEnd, decimal plannedQuantity,
        MomWorkOrderSourceKind sourceKind, string? sourceDocumentNo, Guid? salesOrderId, Guid? pmsProjectId)
    {
        if (string.IsNullOrWhiteSpace(workOrderNo)) throw new ArgumentException("制造工单号不能为空。", nameof(workOrderNo));
        if (productId == Guid.Empty) throw new ArgumentException("必须选择制造商品。", nameof(productId));
        if (plannedEnd < plannedStart) throw new ArgumentException("计划结束日期不能早于开始日期。", nameof(plannedEnd));
        if (plannedQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(plannedQuantity), "计划数量必须大于 0。");
        if (sourceKind != MomWorkOrderSourceKind.Manual && string.IsNullOrWhiteSpace(sourceDocumentNo)) throw new ArgumentException("非手工制造工单必须填写来源单号。", nameof(sourceDocumentNo));
        if (sourceKind == MomWorkOrderSourceKind.SalesOrder && salesOrderId is null) throw new ArgumentException("销售订单来源必须绑定销售订单。", nameof(salesOrderId));
        if (sourceKind == MomWorkOrderSourceKind.PmsProject && pmsProjectId is null) throw new ArgumentException("PMS 项目来源必须绑定项目。", nameof(pmsProjectId));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
