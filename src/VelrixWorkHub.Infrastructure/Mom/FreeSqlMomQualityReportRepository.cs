using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomQualityReportRepository(IFreeSql fsql) : IMomQualityReportRepository
{
    public IReadOnlyList<MomQualityReport> List() => fsql.Select<MomQualityReportRecord>().OrderByDescending(x => x.CreatedOn).OrderByDescending(x => x.ReportNo).ToList().Select(ToDomain).ToArray();
    public void Add(MomQualityReport item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(MomQualityReport item) => fsql.Update<MomQualityReportRecord>().SetSource(ToRecord(item)).Where(x => x.Id == item.Id).ExecuteAffrows();

    private static MomQualityReport ToDomain(MomQualityReportRecord x) => MomQualityReport.Restore(x.Id, x.InspectionId, x.WorkOrderId, x.OperationId, x.ProductId,
        x.InspectionType, x.InspectionStatus, x.ReportNo, x.InspectionNo, x.StandardCode, x.StandardVersion, x.BatchNo, x.SerialNo,
        x.SampleQuantity, x.AcceptedQuantity, x.RejectedQuantity, x.Conclusion, x.SnapshotJson, x.Status, x.CreatedBy, x.CreatedOn,
        x.PublishedBy, x.PublishedOn, x.VoidedBy, x.VoidedOn, x.Notes, x.OtherInfo);

    private static MomQualityReportRecord ToRecord(MomQualityReport x) => new()
    {
        Id = x.Id, InspectionId = x.InspectionId, WorkOrderId = x.WorkOrderId, OperationId = x.OperationId, ProductId = x.ProductId,
        InspectionType = x.InspectionType, InspectionStatus = x.InspectionStatus, ReportNo = x.ReportNo, InspectionNo = x.InspectionNo,
        StandardCode = x.StandardCode, StandardVersion = x.StandardVersion, BatchNo = x.BatchNo, SerialNo = x.SerialNo,
        SampleQuantity = x.SampleQuantity, AcceptedQuantity = x.AcceptedQuantity, RejectedQuantity = x.RejectedQuantity,
        Conclusion = x.Conclusion, SnapshotJson = x.SnapshotJson, Status = x.Status, CreatedBy = x.CreatedBy, CreatedOn = x.CreatedOn,
        PublishedBy = x.PublishedBy, PublishedOn = x.PublishedOn, VoidedBy = x.VoidedBy, VoidedOn = x.VoidedOn, Notes = x.Notes, OtherInfo = x.OtherInfo
    };
}
