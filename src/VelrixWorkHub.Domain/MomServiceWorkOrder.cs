namespace VelrixWorkHub.Domain;

public enum MomServiceWorkOrderType
{
    Installation,
    Repair
}

public enum MomServiceWorkOrderStatus
{
    Draft,
    Scheduled,
    InProgress,
    Completed,
    Cancelled
}

/// <summary>
/// 客户设备售后服务工单。安装和维修复用同一状态、历史与事务边界；维修完成不改变设备在用状态。
/// </summary>
public sealed class MomServiceWorkOrder
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string WorkOrderNo { get; private set; } = string.Empty;
    public MomServiceWorkOrderType Type { get; private set; }
    public Guid EquipmentId { get; private set; }
    public DateOnly? ScheduledOn { get; private set; }
    public string? AssignedTo { get; private set; }
    public string PlannedLocation { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public MomServiceWorkOrderStatus Status { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTime CreatedOn { get; private set; }
    public string? StartedBy { get; private set; }
    public DateTime? StartedOn { get; private set; }
    public string? CompletedBy { get; private set; }
    public DateTime? CompletedOn { get; private set; }
    public string? CompletionNotes { get; private set; }
    public string? CancelledBy { get; private set; }
    public DateTime? CancelledOn { get; private set; }
    public string? CancellationReason { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomServiceWorkOrder(string workOrderNo, MomServiceWorkOrderType type, Guid equipmentId,
        string plannedLocation, string description, string createdBy, DateTime? createdOn = null,
        string? otherInfo = null, Guid? id = null)
    {
        ValidateIdentity(workOrderNo, type, equipmentId, plannedLocation, description, createdBy);
        Id = id ?? Guid.CreateVersion7(); WorkOrderNo = workOrderNo.Trim(); Type = type; EquipmentId = equipmentId;
        PlannedLocation = plannedLocation.Trim(); Description = description.Trim(); CreatedBy = createdBy.Trim();
        CreatedOn = createdOn ?? DateTime.Now; OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
        Status = MomServiceWorkOrderStatus.Draft;
    }

    public static MomServiceWorkOrder Restore(Guid id, string workOrderNo, MomServiceWorkOrderType type, Guid equipmentId,
        DateOnly? scheduledOn, string? assignedTo, string plannedLocation, string description, MomServiceWorkOrderStatus status,
        string createdBy, DateTime createdOn, string? startedBy, DateTime? startedOn, string? completedBy, DateTime? completedOn,
        string? completionNotes, string? cancelledBy, DateTime? cancelledOn, string? cancellationReason, string? otherInfo)
        => new(workOrderNo, type, equipmentId, plannedLocation, description, createdBy, createdOn, otherInfo, id)
        {
            ScheduledOn = scheduledOn, AssignedTo = Clean(assignedTo), Status = status, StartedBy = Clean(startedBy),
            StartedOn = startedOn, CompletedBy = Clean(completedBy), CompletedOn = completedOn,
            CompletionNotes = Clean(completionNotes), CancelledBy = Clean(cancelledBy), CancelledOn = cancelledOn,
            CancellationReason = Clean(cancellationReason)
        };

    public void Schedule(DateOnly scheduledOn, string assignedTo)
    {
        if (Status != MomServiceWorkOrderStatus.Draft) throw new InvalidOperationException("只有草稿服务工单可以安排。 ");
        if (scheduledOn < DateOnly.FromDateTime(CreatedOn.Date)) throw new ArgumentException(
            Type == MomServiceWorkOrderType.Repair ? "计划维修日期不能早于工单创建日期。" : "计划安装日期不能早于工单创建日期。", nameof(scheduledOn));
        if (string.IsNullOrWhiteSpace(assignedTo)) throw new ArgumentException(
            Type == MomServiceWorkOrderType.Repair ? "维修负责人不能为空。" : "安装负责人不能为空。", nameof(assignedTo));
        ScheduledOn = scheduledOn; AssignedTo = assignedTo.Trim(); Status = MomServiceWorkOrderStatus.Scheduled;
    }

    public void Start(string actor, DateTime? startedOn = null)
    {
        if (Status != MomServiceWorkOrderStatus.Scheduled) throw new InvalidOperationException("只有已安排的服务工单可以开始。 ");
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("操作人不能为空。", nameof(actor));
        Status = MomServiceWorkOrderStatus.InProgress; StartedBy = actor.Trim(); StartedOn = startedOn ?? DateTime.Now;
    }

    public void Complete(string actor, string? notes = null, DateTime? completedOn = null)
    {
        if (Status != MomServiceWorkOrderStatus.InProgress) throw new InvalidOperationException("只有进行中的服务工单可以完成。 ");
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("操作人不能为空。", nameof(actor));
        Status = MomServiceWorkOrderStatus.Completed; CompletedBy = actor.Trim(); CompletedOn = completedOn ?? DateTime.Now;
        CompletionNotes = Clean(notes);
    }

    public void Cancel(string actor, string reason, DateTime? cancelledOn = null)
    {
        if (Status is MomServiceWorkOrderStatus.Completed or MomServiceWorkOrderStatus.Cancelled)
            throw new InvalidOperationException("已完成或已取消的服务工单不能再次取消。 ");
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("操作人不能为空。", nameof(actor));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("取消原因不能为空。", nameof(reason));
        Status = MomServiceWorkOrderStatus.Cancelled; CancelledBy = actor.Trim(); CancelledOn = cancelledOn ?? DateTime.Now;
        CancellationReason = reason.Trim();
    }

    public void RestoreLifecycle(MomServiceWorkOrderStatus status, DateOnly? scheduledOn, string? assignedTo,
        string? startedBy, DateTime? startedOn, string? completedBy, DateTime? completedOn, string? completionNotes,
        string? cancelledBy, DateTime? cancelledOn, string? cancellationReason)
    {
        Status = status; ScheduledOn = scheduledOn; AssignedTo = Clean(assignedTo); StartedBy = Clean(startedBy); StartedOn = startedOn;
        CompletedBy = Clean(completedBy); CompletedOn = completedOn; CompletionNotes = Clean(completionNotes);
        CancelledBy = Clean(cancelledBy); CancelledOn = cancelledOn; CancellationReason = Clean(cancellationReason);
    }

    private static void ValidateIdentity(string workOrderNo, MomServiceWorkOrderType type, Guid equipmentId,
        string plannedLocation, string description, string createdBy)
    {
        if (string.IsNullOrWhiteSpace(workOrderNo)) throw new ArgumentException("服务工单编号不能为空。", nameof(workOrderNo));
        if (workOrderNo.Trim().Length > 80) throw new ArgumentException("服务工单编号最多 80 个字符。", nameof(workOrderNo));
        if (equipmentId == Guid.Empty) throw new ArgumentException("客户设备不能为空。", nameof(equipmentId));
        if (string.IsNullOrWhiteSpace(plannedLocation)) throw new ArgumentException(
            type == MomServiceWorkOrderType.Repair ? "维修现场位置不能为空。" : "计划安装位置不能为空。", nameof(plannedLocation));
        if (plannedLocation.Trim().Length > 300) throw new ArgumentException(
            type == MomServiceWorkOrderType.Repair ? "维修现场位置最多 300 个字符。" : "计划安装位置最多 300 个字符。", nameof(plannedLocation));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("服务工单说明不能为空。", nameof(description));
        if (description.Trim().Length > 2000) throw new ArgumentException("服务工单说明最多 2000 个字符。", nameof(description));
        if (string.IsNullOrWhiteSpace(createdBy)) throw new ArgumentException("创建人不能为空。", nameof(createdBy));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public enum MomServiceWorkOrderHistoryAction
{
    Created,
    Scheduled,
    Started,
    Completed,
    Cancelled
}

public sealed class MomServiceWorkOrderHistory
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid WorkOrderId { get; private set; }
    public MomServiceWorkOrderHistoryAction Action { get; private set; }
    public MomServiceWorkOrderStatus ToStatus { get; private set; }
    public string Actor { get; private set; } = string.Empty;
    public DateTime OccurredOn { get; private set; }
    public string? Note { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomServiceWorkOrderHistory(Guid workOrderId, MomServiceWorkOrderHistoryAction action,
        MomServiceWorkOrderStatus toStatus, string actor, DateTime? occurredOn = null, string? note = null,
        string? otherInfo = null, Guid? id = null)
    {
        if (workOrderId == Guid.Empty) throw new ArgumentException("服务工单不能为空。", nameof(workOrderId));
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("操作人不能为空。", nameof(actor));
        Id = id ?? Guid.CreateVersion7(); WorkOrderId = workOrderId; Action = action; ToStatus = toStatus;
        Actor = actor.Trim(); OccurredOn = occurredOn ?? DateTime.Now; Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public static MomServiceWorkOrderHistory Restore(Guid id, Guid workOrderId, MomServiceWorkOrderHistoryAction action,
        MomServiceWorkOrderStatus toStatus, string actor, DateTime occurredOn, string? note, string? otherInfo)
        => new(workOrderId, action, toStatus, actor, occurredOn, note, otherInfo, id);
}
