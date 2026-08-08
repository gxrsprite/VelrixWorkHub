using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomWorkOrderOperationWorkLogRepository(IFreeSql fsql) : IMomWorkOrderOperationWorkLogRepository
{
    public IReadOnlyList<MomWorkOrderOperationWorkLog> List() => fsql.Select<MomWorkOrderOperationWorkLogRecord>()
        .OrderByDescending(x => x.StartedOn).ToList().Select(ToDomain).ToArray();

    public void Add(MomWorkOrderOperationWorkLog item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    private static MomWorkOrderOperationWorkLog ToDomain(MomWorkOrderOperationWorkLogRecord x) =>
        MomWorkOrderOperationWorkLog.Restore(x.Id, x.OperationId, x.WorkOrderId, x.WorkCenterId, x.OperatorUserId,
            x.OperatorName, x.EquipmentId, x.EquipmentName, x.StartedOn, x.EndedOn, x.Hours, x.SourceNo, x.Notes, x.OtherInfo);

    private static MomWorkOrderOperationWorkLogRecord ToRecord(MomWorkOrderOperationWorkLog x) => new()
    {
        Id = x.Id, OperationId = x.OperationId, WorkOrderId = x.WorkOrderId, WorkCenterId = x.WorkCenterId,
        OperatorUserId = x.OperatorUserId, OperatorName = x.OperatorName, EquipmentId = x.EquipmentId, EquipmentName = x.EquipmentName, StartedOn = x.StartedOn, EndedOn = x.EndedOn,
        Hours = x.Hours, SourceNo = x.SourceNo, Notes = x.Notes, OtherInfo = x.OtherInfo
    };
}
