using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomWorkOrderOperationRepository(IFreeSql fsql) : IMomWorkOrderOperationRepository
{
    public IReadOnlyList<MomWorkOrderOperation> List() => fsql.Select<MomWorkOrderOperationRecord>()
        .OrderBy(x => x.WorkOrderId).OrderBy(x => x.OperationSequence).ToList().Select(ToDomain).ToArray();

    public void Add(MomWorkOrderOperation item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    public void Update(MomWorkOrderOperation item)
    {
        var rows = fsql.Update<MomWorkOrderOperationRecord>()
            .Set(x => x.ReportedQuantity, item.ReportedQuantity).Set(x => x.GoodQuantity, item.GoodQuantity).Set(x => x.ScrapQuantity, item.ScrapQuantity)
            .Set(x => x.Status, item.Status).Set(x => x.AcceptedBy, item.AcceptedBy).Set(x => x.AcceptedOn, item.AcceptedOn)
            .Set(x => x.StartedOn, item.StartedOn).Set(x => x.PausedOn, item.PausedOn).Set(x => x.CompletedOn, item.CompletedOn)
            .Where(x => x.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("工序不存在或已被删除。");
    }

    private static MomWorkOrderOperation ToDomain(MomWorkOrderOperationRecord x) => MomWorkOrderOperation.Restore(x.Id, x.WorkOrderId, x.OperationSequence,
        x.OperationCode, x.OperationName, x.WorkCenterId, x.PlannedQuantity, x.ReportedQuantity, x.GoodQuantity, x.ScrapQuantity,
        x.Status, x.AcceptedBy, x.AcceptedOn, x.StartedOn, x.PausedOn, x.CompletedOn, x.OtherInfo,
        x.StandardSetupHours, x.StandardRunHoursPerUnit);

    private static MomWorkOrderOperationRecord ToRecord(MomWorkOrderOperation x) => new()
    {
        Id = x.Id, WorkOrderId = x.WorkOrderId, OperationSequence = x.OperationSequence, OperationCode = x.OperationCode,
        OperationName = x.OperationName, WorkCenterId = x.WorkCenterId, PlannedQuantity = x.PlannedQuantity,
        ReportedQuantity = x.ReportedQuantity, GoodQuantity = x.GoodQuantity, ScrapQuantity = x.ScrapQuantity, Status = x.Status,
        AcceptedBy = x.AcceptedBy, AcceptedOn = x.AcceptedOn, StartedOn = x.StartedOn, PausedOn = x.PausedOn, CompletedOn = x.CompletedOn, OtherInfo = x.OtherInfo,
        StandardSetupHours = x.StandardSetupHours, StandardRunHoursPerUnit = x.StandardRunHoursPerUnit
    };
}
