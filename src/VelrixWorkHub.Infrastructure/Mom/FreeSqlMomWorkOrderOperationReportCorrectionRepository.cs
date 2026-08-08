using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomWorkOrderOperationReportCorrectionRepository(IFreeSql fsql) : IMomWorkOrderOperationReportCorrectionRepository
{
    public IReadOnlyList<MomWorkOrderOperationReportCorrection> List() => fsql.Select<MomWorkOrderOperationReportCorrectionRecord>()
        .OrderBy(x => x.OperationId).OrderByDescending(x => x.OccurredOn).ToList().Select(ToDomain).ToArray();

    public void Add(MomWorkOrderOperationReportCorrection item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    private static MomWorkOrderOperationReportCorrection ToDomain(MomWorkOrderOperationReportCorrectionRecord x)
        => MomWorkOrderOperationReportCorrection.Restore(x.Id, x.ReportId, x.OperationId, x.WorkOrderId, x.WorkCenterId,
            x.GoodQuantity, x.ScrapQuantity, x.SourceNo, x.OccurredOn, x.Actor, x.Notes, x.OtherInfo);

    private static MomWorkOrderOperationReportCorrectionRecord ToRecord(MomWorkOrderOperationReportCorrection x) => new()
    {
        Id = x.Id, ReportId = x.ReportId, OperationId = x.OperationId, WorkOrderId = x.WorkOrderId, WorkCenterId = x.WorkCenterId,
        Quantity = x.Quantity, GoodQuantity = x.GoodQuantity, ScrapQuantity = x.ScrapQuantity, SourceNo = x.SourceNo,
        OccurredOn = x.OccurredOn, Actor = x.Actor, Notes = x.Notes, OtherInfo = x.OtherInfo
    };
}
