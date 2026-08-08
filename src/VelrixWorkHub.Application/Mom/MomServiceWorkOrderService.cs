using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

/// <summary>
/// MOM-08H/MOM-08I 售后服务工单。安装完成时由本服务调用售后设备 Application 用例推进设备安装，
/// 维修完成只更新维修工单；本服务不直接写设备表，工单、设备档案和两类历史记录共享事务边界。
/// </summary>
public sealed class MomServiceWorkOrderService(
    IMomServiceWorkOrderRepository repository,
    IMomServiceWorkOrderHistoryRepository historyRepository,
    MomServiceEquipmentService equipmentService,
    IWorkflowTransactionBoundary? transactions = null)
{
    public IReadOnlyList<MomServiceWorkOrder> List(Guid? equipmentId = null, MomServiceWorkOrderStatus? status = null, string? keyword = null)
    {
        var text = keyword?.Trim();
        return repository.List(equipmentId)
            .Where(x => (status is null || x.Status == status) && (string.IsNullOrWhiteSpace(text)
                || x.WorkOrderNo.Contains(text, StringComparison.OrdinalIgnoreCase)
                || x.Description.Contains(text, StringComparison.OrdinalIgnoreCase)
                || (x.AssignedTo?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)))
            .OrderBy(x => x.Status).ThenByDescending(x => x.CreatedOn).ThenBy(x => x.WorkOrderNo).ToArray();
    }

    public MomServiceWorkOrder? Get(Guid id) => repository.Get(id);

    public IReadOnlyList<MomServiceWorkOrderHistory> ListHistory(Guid workOrderId)
        => historyRepository.List(workOrderId).OrderByDescending(x => x.OccurredOn).ToArray();

    public MomServiceWorkOrder CreateInstallation(Guid equipmentId, string workOrderNo, DateOnly scheduledOn,
        string assignedTo, string plannedLocation, string description, string actor, string? otherInfo = null)
        => Create(MomServiceWorkOrderType.Installation, MomServiceEquipmentStatus.PendingInstallation, equipmentId, workOrderNo,
            scheduledOn, assignedTo, plannedLocation, description, actor, otherInfo);

    public MomServiceWorkOrder CreateRepair(Guid equipmentId, string workOrderNo, DateOnly scheduledOn,
        string assignedTo, string plannedLocation, string description, string actor, string? otherInfo = null)
        => Create(MomServiceWorkOrderType.Repair, MomServiceEquipmentStatus.Active, equipmentId, workOrderNo,
            scheduledOn, assignedTo, plannedLocation, description, actor, otherInfo);

    private MomServiceWorkOrder Create(MomServiceWorkOrderType type, MomServiceEquipmentStatus requiredEquipmentStatus,
        Guid equipmentId, string workOrderNo, DateOnly scheduledOn, string assignedTo, string plannedLocation,
        string description, string actor, string? otherInfo)
    {
        var equipment = FindEquipment(equipmentId);
        if (equipment.Status != requiredEquipmentStatus)
            throw new InvalidOperationException(type == MomServiceWorkOrderType.Installation
                ? "只有待安装设备可以创建安装工单。 " : "只有在用设备可以创建维修工单。 ");
        if (repository.List(equipmentId).Any(x => x.Status is MomServiceWorkOrderStatus.Draft or MomServiceWorkOrderStatus.Scheduled or MomServiceWorkOrderStatus.InProgress))
            throw new InvalidOperationException("客户设备已有未完成服务工单。 ");
        var item = new MomServiceWorkOrder(workOrderNo, type, equipmentId, plannedLocation, description, actor, otherInfo: otherInfo);
        var created = new MomServiceWorkOrderHistory(item.Id, MomServiceWorkOrderHistoryAction.Created, item.Status, actor, item.CreatedOn);
        item.Schedule(scheduledOn, assignedTo);
        var scheduled = new MomServiceWorkOrderHistory(item.Id, MomServiceWorkOrderHistoryAction.Scheduled, item.Status, actor, item.CreatedOn, $"计划日期：{scheduledOn:yyyy-MM-dd}");
        Persist(() => { repository.Add(item); historyRepository.Add(created); historyRepository.Add(scheduled); });
        return item;
    }

    public void Start(Guid workOrderId, string actor)
    {
        var item = Find(workOrderId); var snapshot = Snapshot(item); var now = DateTime.Now;
        item.Start(actor, now);
        Persist(() => { repository.Update(item); historyRepository.Add(new MomServiceWorkOrderHistory(item.Id, MomServiceWorkOrderHistoryAction.Started, item.Status, actor, now)); },
            _ => Restore(item, snapshot));
    }

    public void CompleteInstallation(Guid workOrderId, string actor, DateOnly installedOn, string? notes = null)
    {
        var item = Find(workOrderId);
        if (item.Type != MomServiceWorkOrderType.Installation) throw new InvalidOperationException("当前服务工单不是安装工单。 ");
        if (item.Status != MomServiceWorkOrderStatus.InProgress) throw new InvalidOperationException("只有进行中的安装工单可以完成。 ");
        var equipment = FindEquipment(item.EquipmentId);
        var snapshot = Snapshot(item); var completedOn = DateTime.Now;
        void Core()
        {
            equipmentService.Install(equipment.Id, actor, installedOn, item.PlannedLocation);
            item.Complete(actor, notes, completedOn);
            repository.Update(item);
            historyRepository.Add(new MomServiceWorkOrderHistory(item.Id, MomServiceWorkOrderHistoryAction.Completed, item.Status, actor, completedOn, notes));
        }
        Persist(Core, _ => Restore(item, snapshot));
    }

    public void CompleteRepair(Guid workOrderId, string actor, string? notes = null)
    {
        var item = Find(workOrderId);
        if (item.Type != MomServiceWorkOrderType.Repair) throw new InvalidOperationException("当前服务工单不是维修工单。 ");
        if (item.Status != MomServiceWorkOrderStatus.InProgress) throw new InvalidOperationException("只有进行中的维修工单可以完成。 ");
        _ = FindEquipment(item.EquipmentId);
        var snapshot = Snapshot(item); var completedOn = DateTime.Now;
        item.Complete(actor, notes, completedOn);
        Persist(() => { repository.Update(item); historyRepository.Add(new MomServiceWorkOrderHistory(item.Id, MomServiceWorkOrderHistoryAction.Completed, item.Status, actor, completedOn, notes)); },
            _ => Restore(item, snapshot));
    }

    public void Cancel(Guid workOrderId, string actor, string reason)
    {
        var item = Find(workOrderId); var snapshot = Snapshot(item); var cancelledOn = DateTime.Now;
        item.Cancel(actor, reason, cancelledOn);
        Persist(() => { repository.Update(item); historyRepository.Add(new MomServiceWorkOrderHistory(item.Id, MomServiceWorkOrderHistoryAction.Cancelled, item.Status, actor, cancelledOn, reason)); },
            _ => Restore(item, snapshot));
    }

    private MomServiceWorkOrder Find(Guid id) => repository.Get(id) ?? throw new InvalidOperationException("服务工单不存在。 ");

    private MomServiceEquipment FindEquipment(Guid id) => equipmentService.List().FirstOrDefault(x => x.Id == id)
        ?? throw new InvalidOperationException("售后设备档案不存在。 ");

    private void Persist(Action operation, Action<Exception>? rollback = null)
    {
        if (transactions is null) operation();
        else transactions.Execute(operation, rollback);
    }

    private static WorkOrderSnapshot Snapshot(MomServiceWorkOrder item) => new(item.Status, item.ScheduledOn, item.AssignedTo,
        item.StartedBy, item.StartedOn, item.CompletedBy, item.CompletedOn, item.CompletionNotes, item.CancelledBy, item.CancelledOn, item.CancellationReason);

    private static void Restore(MomServiceWorkOrder item, WorkOrderSnapshot snapshot) => item.RestoreLifecycle(snapshot.Status, snapshot.ScheduledOn,
        snapshot.AssignedTo, snapshot.StartedBy, snapshot.StartedOn, snapshot.CompletedBy, snapshot.CompletedOn, snapshot.CompletionNotes,
        snapshot.CancelledBy, snapshot.CancelledOn, snapshot.CancellationReason);

    private sealed record WorkOrderSnapshot(MomServiceWorkOrderStatus Status, DateOnly? ScheduledOn, string? AssignedTo, string? StartedBy,
        DateTime? StartedOn, string? CompletedBy, DateTime? CompletedOn, string? CompletionNotes, string? CancelledBy,
        DateTime? CancelledOn, string? CancellationReason);
}
