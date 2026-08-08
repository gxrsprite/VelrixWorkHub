using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomServiceWorkOrderRepository(IFreeSql fsql) : IMomServiceWorkOrderRepository
{
    public IReadOnlyList<MomServiceWorkOrder> List(Guid? equipmentId = null)
    {
        var query = fsql.Select<MomServiceWorkOrderRecord>();
        if (equipmentId is Guid id) query = query.Where(x => x.EquipmentId == id);
        return query.OrderBy(x => x.Status).OrderByDescending(x => x.CreatedOn).ToList().Select(ToDomain).ToArray();
    }

    public MomServiceWorkOrder? Get(Guid id) => fsql.Select<MomServiceWorkOrderRecord>().Where(x => x.Id == id).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(MomServiceWorkOrder item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    public void Update(MomServiceWorkOrder item)
    {
        var rows = fsql.Update<MomServiceWorkOrderRecord>().SetSource(ToRecord(item)).Where(x => x.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("服务工单不存在或已被删除。 ");
    }

    private static MomServiceWorkOrder ToDomain(MomServiceWorkOrderRecord x) => MomServiceWorkOrder.Restore(x.Id, x.WorkOrderNo, x.Type, x.EquipmentId,
        x.ScheduledOn is DateTime scheduledOn ? DateOnly.FromDateTime(scheduledOn) : null, x.AssignedTo, x.PlannedLocation, x.Description,
        x.Status, x.CreatedBy, x.CreatedOn, x.StartedBy, x.StartedOn, x.CompletedBy, x.CompletedOn, x.CompletionNotes,
        x.CancelledBy, x.CancelledOn, x.CancellationReason, x.OtherInfo);

    private static MomServiceWorkOrderRecord ToRecord(MomServiceWorkOrder x) => new()
    {
        Id = x.Id, WorkOrderNo = x.WorkOrderNo, Type = x.Type, EquipmentId = x.EquipmentId,
        ScheduledOn = x.ScheduledOn?.ToDateTime(TimeOnly.MinValue), AssignedTo = x.AssignedTo, PlannedLocation = x.PlannedLocation,
        Description = x.Description, Status = x.Status, CreatedBy = x.CreatedBy, CreatedOn = x.CreatedOn,
        StartedBy = x.StartedBy, StartedOn = x.StartedOn, CompletedBy = x.CompletedBy, CompletedOn = x.CompletedOn,
        CompletionNotes = x.CompletionNotes, CancelledBy = x.CancelledBy, CancelledOn = x.CancelledOn,
        CancellationReason = x.CancellationReason, OtherInfo = x.OtherInfo
        , OpenKey = x.Status is MomServiceWorkOrderStatus.Draft or MomServiceWorkOrderStatus.Scheduled or MomServiceWorkOrderStatus.InProgress
            ? x.EquipmentId.ToString("N") : x.Id.ToString("N")
    };
}

public sealed class FreeSqlMomServiceWorkOrderHistoryRepository(IFreeSql fsql) : IMomServiceWorkOrderHistoryRepository
{
    public IReadOnlyList<MomServiceWorkOrderHistory> List(Guid workOrderId)
        => fsql.Select<MomServiceWorkOrderHistoryRecord>().Where(x => x.WorkOrderId == workOrderId)
            .OrderByDescending(x => x.OccurredOn).ToList().Select(ToDomain).ToArray();

    public void Add(MomServiceWorkOrderHistory item) => fsql.Insert(new MomServiceWorkOrderHistoryRecord
    {
        Id = item.Id, WorkOrderId = item.WorkOrderId, Action = item.Action, ToStatus = item.ToStatus,
        Actor = item.Actor, OccurredOn = item.OccurredOn, Note = item.Note, OtherInfo = item.OtherInfo
    }).ExecuteAffrows();

    private static MomServiceWorkOrderHistory ToDomain(MomServiceWorkOrderHistoryRecord x)
        => MomServiceWorkOrderHistory.Restore(x.Id, x.WorkOrderId, x.Action, x.ToStatus, x.Actor, x.OccurredOn, x.Note, x.OtherInfo);
}
