namespace VelrixWorkHub.Domain;

public enum MomAcceptanceType { Fat, Sat }
public enum MomAcceptanceStatus { Draft, Submitted, Passed, Failed, Cancelled }

/// <summary>
/// MOM-08E FAT/SAT 验收记录。记录保存销售订单/发运来源的稳定快照引用，检查项结果单独保存。
/// </summary>
public sealed class MomAcceptance
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string AcceptanceNo { get; private set; } = string.Empty;
    public MomAcceptanceType AcceptanceType { get; private set; }
    public MomAcceptanceStatus Status { get; private set; }
    public Guid SalesOrderId { get; private set; }
    public Guid? ShipmentId { get; private set; }
    public Guid? PmsProjectId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid ProductId { get; private set; }
    public string? SerialNo { get; private set; }
    public DateOnly PlannedDate { get; private set; }
    public string? LocationOrMode { get; private set; }
    public string? Participants { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTime CreatedOn { get; private set; }
    public string? SubmittedBy { get; private set; }
    public DateTime? SubmittedOn { get; private set; }
    public string? CompletedBy { get; private set; }
    public DateTime? CompletedOn { get; private set; }
    public string? Conclusion { get; private set; }
    public string? FailureReason { get; private set; }
    public string? CancelledBy { get; private set; }
    public DateTime? CancelledOn { get; private set; }
    public string? CancellationReason { get; private set; }
    public string? Notes { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomAcceptance(MomAcceptanceType acceptanceType, Guid salesOrderId, Guid? shipmentId, Guid? pmsProjectId,
        Guid customerId, Guid productId, DateOnly plannedDate, string createdBy, string? serialNo = null,
        string? locationOrMode = null, string? participants = null, string? notes = null, string? otherInfo = null,
        Guid? id = null, string? acceptanceNo = null)
    {
        if (!Enum.IsDefined(acceptanceType)) throw new ArgumentOutOfRangeException(nameof(acceptanceType), "验收类型无效。");
        if (salesOrderId == Guid.Empty) throw new ArgumentException("销售订单不能为空。", nameof(salesOrderId));
        if (shipmentId == Guid.Empty) throw new ArgumentException("发运记录无效。", nameof(shipmentId));
        if (pmsProjectId == Guid.Empty) throw new ArgumentException("PMS 项目无效。", nameof(pmsProjectId));
        if (customerId == Guid.Empty) throw new ArgumentException("客户不能为空。", nameof(customerId));
        if (productId == Guid.Empty) throw new ArgumentException("验收商品不能为空。", nameof(productId));
        if (string.IsNullOrWhiteSpace(createdBy)) throw new ArgumentException("验收记录创建人不能为空。", nameof(createdBy));

        Id = id ?? Guid.CreateVersion7();
        AcceptanceNo = string.IsNullOrWhiteSpace(acceptanceNo) ? BuildAcceptanceNo(acceptanceType, Id) : NormalizeText(acceptanceNo, 80, "验收单号");
        AcceptanceType = acceptanceType; Status = MomAcceptanceStatus.Draft; SalesOrderId = salesOrderId;
        ShipmentId = shipmentId; PmsProjectId = pmsProjectId; CustomerId = customerId; ProductId = productId;
        SerialNo = NormalizeOptional(serialNo, 100, "序列号"); PlannedDate = plannedDate;
        LocationOrMode = NormalizeOptional(locationOrMode, 200, "验收地点/方式");
        Participants = NormalizeOptional(participants, 500, "验收参与人");
        CreatedBy = NormalizeText(createdBy, 100, "创建人"); CreatedOn = DateTime.Now;
        Notes = NormalizeOptional(notes, 1000, "验收备注"); OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public static MomAcceptance Restore(Guid id, string acceptanceNo, MomAcceptanceType acceptanceType, MomAcceptanceStatus status,
        Guid salesOrderId, Guid? shipmentId, Guid? pmsProjectId, Guid customerId, Guid productId, string? serialNo,
        DateOnly plannedDate, string? locationOrMode, string? participants, string createdBy, DateTime createdOn,
        string? submittedBy, DateTime? submittedOn, string? completedBy, DateTime? completedOn, string? conclusion,
        string? failureReason, string? cancelledBy, DateTime? cancelledOn, string? cancellationReason, string? notes, string? otherInfo)
    {
        var item = new MomAcceptance(acceptanceType, salesOrderId, shipmentId, pmsProjectId, customerId, productId, plannedDate,
            createdBy, serialNo, locationOrMode, participants, notes, otherInfo, id, acceptanceNo);
        item.CreatedOn = createdOn; item.Status = status; item.SubmittedBy = Clean(submittedBy); item.SubmittedOn = submittedOn;
        item.CompletedBy = Clean(completedBy); item.CompletedOn = completedOn; item.Conclusion = Clean(conclusion);
        item.FailureReason = Clean(failureReason); item.CancelledBy = Clean(cancelledBy); item.CancelledOn = cancelledOn;
        item.CancellationReason = Clean(cancellationReason);
        return item;
    }

    public void Submit(string actor, DateTime submittedOn)
    {
        if (Status != MomAcceptanceStatus.Draft) throw new InvalidOperationException("只有草稿验收单可以提交。");
        SubmittedBy = NormalizeText(actor, 100, "提交人"); SubmittedOn = submittedOn; Status = MomAcceptanceStatus.Submitted;
    }

    public void Complete(MomAcceptanceStatus result, string actor, DateTime completedOn, string conclusion, string? failureReason)
    {
        if (Status != MomAcceptanceStatus.Submitted) throw new InvalidOperationException("只有已提交验收单可以完成验收。");
        if (result is not (MomAcceptanceStatus.Passed or MomAcceptanceStatus.Failed)) throw new ArgumentOutOfRangeException(nameof(result), "验收结果无效。");
        Conclusion = NormalizeText(conclusion, 1000, "验收结论");
        FailureReason = result == MomAcceptanceStatus.Failed ? NormalizeText(failureReason, 1000, "失败原因") : Clean(failureReason);
        CompletedBy = NormalizeText(actor, 100, "完成人"); CompletedOn = completedOn; Status = result;
    }

    public void Cancel(string actor, DateTime cancelledOn, string reason)
    {
        if (Status is not (MomAcceptanceStatus.Draft or MomAcceptanceStatus.Submitted)) throw new InvalidOperationException("只有草稿或已提交验收单可以取消。");
        CancelledBy = NormalizeText(actor, 100, "取消人"); CancellationReason = NormalizeText(reason, 1000, "取消原因");
        CancelledOn = cancelledOn; Status = MomAcceptanceStatus.Cancelled;
    }

    public void RestoreState(MomAcceptanceStatus status, string? submittedBy, DateTime? submittedOn, string? completedBy,
        DateTime? completedOn, string? conclusion, string? failureReason, string? cancelledBy, DateTime? cancelledOn, string? cancellationReason)
    {
        Status = status; SubmittedBy = Clean(submittedBy); SubmittedOn = submittedOn; CompletedBy = Clean(completedBy);
        CompletedOn = completedOn; Conclusion = Clean(conclusion); FailureReason = Clean(failureReason);
        CancelledBy = Clean(cancelledBy); CancelledOn = cancelledOn; CancellationReason = Clean(cancellationReason);
    }

    public static string BuildAcceptanceNo(MomAcceptanceType type, Guid id) => $"MOM-{type.ToString().ToUpperInvariant()}-{id:N}";

    private static string NormalizeText(string? value, int maxLength, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{label}不能为空。", nameof(value));
        var result = value.Trim();
        if (result.Length > maxLength) throw new ArgumentException($"{label}最多 {maxLength} 个字符。", nameof(value));
        return result;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var result = value.Trim();
        if (result.Length > maxLength) throw new ArgumentException($"{label}最多 {maxLength} 个字符。", nameof(value));
        return result;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
