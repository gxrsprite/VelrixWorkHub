using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomWorkOrderOperationReportRepository(IFreeSql fsql) : IMomWorkOrderOperationReportRepository
{
    public IReadOnlyList<MomWorkOrderOperationReport> List() => fsql.Select<MomWorkOrderOperationReportRecord>()
        .OrderBy(x => x.OperationId).OrderByDescending(x => x.OccurredOn).ToList().Select(ToDomain).ToArray();

    public void Add(MomWorkOrderOperationReport item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    private static MomWorkOrderOperationReport ToDomain(MomWorkOrderOperationReportRecord x) => MomWorkOrderOperationReport.Restore(x.Id, x.OperationId,
        x.WorkOrderId, x.WorkCenterId, x.GoodQuantity, x.ScrapQuantity, x.SourceNo, x.OccurredOn, x.Actor, x.Notes, x.OtherInfo);

    private static MomWorkOrderOperationReportRecord ToRecord(MomWorkOrderOperationReport x) => new()
    {
        Id = x.Id, OperationId = x.OperationId, WorkOrderId = x.WorkOrderId, WorkCenterId = x.WorkCenterId,
        Quantity = x.Quantity, GoodQuantity = x.GoodQuantity, ScrapQuantity = x.ScrapQuantity, SourceNo = x.SourceNo,
        OccurredOn = x.OccurredOn, Actor = x.Actor, Notes = x.Notes, OtherInfo = x.OtherInfo
    };
}
