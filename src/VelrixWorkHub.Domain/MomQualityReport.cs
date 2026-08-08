using System.Text.Json;

namespace VelrixWorkHub.Domain;

public enum MomQualityReportStatus { Draft, Published, Voided }

/// <summary>
/// MOM-07D 质量报告。报告是已完成检验结果的可发布快照，附件通过通用业务附件服务挂在报告 Id 下。
/// </summary>
public sealed class MomQualityReport
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid InspectionId { get; private set; }
    public Guid WorkOrderId { get; private set; }
    public Guid? OperationId { get; private set; }
    public Guid? ProductId { get; private set; }
    public MomQualityInspectionType InspectionType { get; private set; }
    public MomQualityInspectionStatus InspectionStatus { get; private set; }
    public string ReportNo { get; private set; } = string.Empty;
    public string InspectionNo { get; private set; } = string.Empty;
    public string? StandardCode { get; private set; }
    public string? StandardVersion { get; private set; }
    public string? BatchNo { get; private set; }
    public string? SerialNo { get; private set; }
    public decimal SampleQuantity { get; private set; }
    public decimal AcceptedQuantity { get; private set; }
    public decimal RejectedQuantity { get; private set; }
    public string Conclusion { get; private set; } = string.Empty;
    public string SnapshotJson { get; private set; } = "{}";
    public MomQualityReportStatus Status { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTime CreatedOn { get; private set; }
    public string? PublishedBy { get; private set; }
    public DateTime? PublishedOn { get; private set; }
    public string? VoidedBy { get; private set; }
    public DateTime? VoidedOn { get; private set; }
    public string? Notes { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomQualityReport(MomQualityInspection inspection, string createdBy, DateTime createdOn, string snapshotJson,
        string? notes = null, string? otherInfo = null, Guid? id = null, string? reportNo = null)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        if (inspection.Status is MomQualityInspectionStatus.Pending or MomQualityInspectionStatus.Cancelled)
            throw new InvalidOperationException("只有已完成的质量检验可以生成质量报告。");
        if (string.IsNullOrWhiteSpace(createdBy)) throw new ArgumentException("质量报告创建人不能为空。", nameof(createdBy));
        Id = id ?? Guid.CreateVersion7();
        InspectionId = inspection.Id; WorkOrderId = inspection.WorkOrderId; OperationId = inspection.OperationId; ProductId = inspection.ProductId;
        InspectionType = inspection.InspectionType; InspectionStatus = inspection.Status;
        InspectionNo = inspection.InspectionNo; ReportNo = string.IsNullOrWhiteSpace(reportNo) ? BuildReportNo(Id) : reportNo.Trim();
        StandardCode = inspection.StandardCode; StandardVersion = inspection.StandardVersion; BatchNo = inspection.BatchNo; SerialNo = inspection.SerialNo;
        SampleQuantity = inspection.SampleQuantity; AcceptedQuantity = inspection.AcceptedQuantity; RejectedQuantity = inspection.RejectedQuantity;
        Conclusion = inspection.Status == MomQualityInspectionStatus.Passed ? "合格" : "不合格";
        SnapshotJson = NormalizeSnapshot(snapshotJson);
        CreatedBy = createdBy.Trim(); CreatedOn = createdOn; Notes = Clean(notes); OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
        Status = MomQualityReportStatus.Draft;
    }

    public static MomQualityReport Restore(Guid id, Guid inspectionId, Guid workOrderId, Guid? operationId, Guid? productId,
        MomQualityInspectionType inspectionType, MomQualityInspectionStatus inspectionStatus, string reportNo, string inspectionNo,
        string? standardCode, string? standardVersion, string? batchNo, string? serialNo, decimal sampleQuantity,
        decimal acceptedQuantity, decimal rejectedQuantity, string conclusion, string snapshotJson, MomQualityReportStatus status,
        string createdBy, DateTime createdOn, string? publishedBy, DateTime? publishedOn, string? voidedBy, DateTime? voidedOn,
        string? notes, string? otherInfo)
    {
        var inspection = new MomQualityInspection(workOrderId, inspectionType, operationId, productId, batchNo, serialNo, sampleQuantity, createdOn,
            standardId: standardCode is null ? null : Guid.CreateVersion7(), standardCode: standardCode, standardVersion: standardVersion,
            standardSnapshotJson: standardCode is null ? null : "{}");
        inspection.RestoreResult(inspectionStatus, acceptedQuantity, rejectedQuantity, null, null);
        var item = new MomQualityReport(inspection, createdBy, createdOn, snapshotJson, notes, otherInfo, id, reportNo);
        item.InspectionId = inspectionId; item.InspectionNo = inspectionNo; item.Conclusion = conclusion; item.Status = status;
        item.PublishedBy = Clean(publishedBy); item.PublishedOn = publishedOn; item.VoidedBy = Clean(voidedBy); item.VoidedOn = voidedOn;
        return item;
    }

    public void Publish(string actor, DateTime publishedOn)
    {
        if (Status != MomQualityReportStatus.Draft) throw new InvalidOperationException("只有草稿质量报告可以发布。");
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("质量报告发布人不能为空。", nameof(actor));
        Status = MomQualityReportStatus.Published; PublishedBy = actor.Trim(); PublishedOn = publishedOn;
    }

    public void Void(string actor, DateTime voidedOn)
    {
        if (Status != MomQualityReportStatus.Published) throw new InvalidOperationException("只有已发布质量报告可以作废。");
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("质量报告作废人不能为空。", nameof(actor));
        Status = MomQualityReportStatus.Voided; VoidedBy = actor.Trim(); VoidedOn = voidedOn;
    }

    public void RestoreState(MomQualityReportStatus status, string? publishedBy, DateTime? publishedOn, string? voidedBy, DateTime? voidedOn)
    {
        Status = status; PublishedBy = Clean(publishedBy); PublishedOn = publishedOn; VoidedBy = Clean(voidedBy); VoidedOn = voidedOn;
    }

    public static string BuildReportNo(Guid id) => $"MQR-{id:N}";

    private static string NormalizeSnapshot(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("质量报告快照不能为空。", nameof(value));
        try { using var document = JsonDocument.Parse(value); return document.RootElement.GetRawText(); }
        catch (JsonException exception) { throw new ArgumentException("质量报告快照必须是有效 JSON。", nameof(value), exception); }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
