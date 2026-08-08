using System.Text.Json;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

/// <summary>
/// MOM-07D 质量报告快照与发布。报告不重新计算检验结果，附件由通用附件服务负责保存和审计。
/// </summary>
public sealed class MomQualityReportService(
    IMomQualityReportRepository repository,
    IMomQualityInspectionRepository inspectionRepository,
    IWorkflowTransactionBoundary? transactions = null)
{
    public const string AttachmentBusinessType = "MomQualityReport";

    public IReadOnlyList<MomQualityReport> List(Guid? inspectionId = null, MomQualityReportStatus? status = null)
    {
        var query = repository.List().AsEnumerable();
        if (inspectionId is Guid selected) query = query.Where(x => x.InspectionId == selected);
        if (status is MomQualityReportStatus selectedStatus) query = query.Where(x => x.Status == selectedStatus);
        return query.OrderByDescending(x => x.CreatedOn).ThenByDescending(x => x.ReportNo).ToArray();
    }

    public MomQualityReport CreateFromInspection(Guid inspectionId, string actor, DateTime? createdOn = null, string? notes = null, string? otherInfo = null)
    {
        var inspection = inspectionRepository.List().FirstOrDefault(x => x.Id == inspectionId)
            ?? throw new InvalidOperationException("质量检验记录不存在。");
        if (repository.List().Any(x => x.InspectionId == inspectionId))
            throw new InvalidOperationException("同一质量检验只能存在一份质量报告。");
        var snapshot = JsonSerializer.Serialize(new
        {
            inspection.InspectionNo, inspection.InspectionType, inspection.Status, inspection.WorkOrderId, inspection.OperationId,
            inspection.ProductId, inspection.StandardCode, inspection.StandardVersion, inspection.StandardSnapshotJson,
            inspection.BatchNo, inspection.SerialNo, inspection.SampleQuantity, inspection.AcceptedQuantity, inspection.RejectedQuantity,
            inspection.Inspector, inspection.InspectedOn, inspection.Notes
        }, JsonSerializationDefaults.CreateWeb());
        var item = new MomQualityReport(inspection, actor, createdOn ?? DateTime.Now, snapshot, notes, otherInfo);
        void Persist() => repository.Add(item);
        if (transactions is null) Persist(); else transactions.Execute(Persist);
        return item;
    }

    public void Publish(Guid reportId, string actor, DateTime? publishedOn = null)
    {
        var item = Find(reportId); var original = Snapshot(item);
        item.Publish(actor, publishedOn ?? DateTime.Now);
        void Persist() => repository.Update(item);
        if (transactions is null) Persist(); else transactions.Execute(Persist, _ => Restore(item, original));
    }

    public void Void(Guid reportId, string actor, DateTime? voidedOn = null)
    {
        var item = Find(reportId); var original = Snapshot(item);
        item.Void(actor, voidedOn ?? DateTime.Now);
        void Persist() => repository.Update(item);
        if (transactions is null) Persist(); else transactions.Execute(Persist, _ => Restore(item, original));
    }

    private MomQualityReport Find(Guid id) => repository.List().FirstOrDefault(x => x.Id == id)
        ?? throw new InvalidOperationException("质量报告不存在。");

    private static ReportSnapshot Snapshot(MomQualityReport item) => new(item.Status, item.PublishedBy, item.PublishedOn, item.VoidedBy, item.VoidedOn);
    private static void Restore(MomQualityReport item, ReportSnapshot snapshot) => item.RestoreState(snapshot.Status, snapshot.PublishedBy, snapshot.PublishedOn, snapshot.VoidedBy, snapshot.VoidedOn);
    private sealed record ReportSnapshot(MomQualityReportStatus Status, string? PublishedBy, DateTime? PublishedOn, string? VoidedBy, DateTime? VoidedOn);
}
