namespace VelrixWorkHub.Domain;

public enum OaAssetRequestStatus
{
    Draft,
    Submitted,
    Approved,
    Rejected,
    Withdrawn,
    Cancelled
}

/// <summary>资产领用申请。审批通过后才会生成实际领用记录。</summary>
public sealed class OaAssetRequest
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid AssetId { get; private set; }
    public Guid ApplicantUserId { get; private set; }
    public string ApplicantName { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public string OtherInfo { get; private set; } = "{}";
    public OaAssetRequestStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public Guid? AssignmentId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public DateTime? ApprovedAt { get; private set; }

    public OaAssetRequest(Guid assetId, Guid applicantUserId, string applicantName, string reason, string? otherInfo, DateTime createdAt)
    {
        if (assetId == Guid.Empty) throw new ArgumentException("申请资产不能为空。", nameof(assetId));
        if (applicantUserId == Guid.Empty) throw new ArgumentException("申请人不能为空。", nameof(applicantUserId));
        AssetId = assetId;
        ApplicantUserId = applicantUserId;
        CreatedAt = createdAt;
        Edit(applicantName, reason, otherInfo);
        Status = OaAssetRequestStatus.Draft;
    }

    public void Edit(string applicantName, string reason, string? otherInfo)
    {
        if (Status is not (OaAssetRequestStatus.Draft or OaAssetRequestStatus.Rejected or OaAssetRequestStatus.Withdrawn))
            throw new InvalidOperationException("只有草稿、驳回或撤回的资产申请可以编辑。");
        ApplicantName = Required(applicantName, "申请人");
        Reason = Required(reason, "申请事由");
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public void Submit(DateTime submittedAt)
    {
        if (Status is not (OaAssetRequestStatus.Draft or OaAssetRequestStatus.Rejected or OaAssetRequestStatus.Withdrawn))
            throw new InvalidOperationException("只有草稿、驳回或撤回的资产申请可以提交。");
        Status = OaAssetRequestStatus.Submitted;
        RejectionReason = null;
        AssignmentId = null;
        SubmittedAt = submittedAt;
        ApprovedAt = null;
    }

    public void Approve(Guid assignmentId, DateTime approvedAt)
    {
        if (Status != OaAssetRequestStatus.Submitted) throw new InvalidOperationException("只有已提交的资产申请可以批准。");
        if (assignmentId == Guid.Empty) throw new ArgumentException("批准后的领用记录不能为空。", nameof(assignmentId));
        Status = OaAssetRequestStatus.Approved;
        AssignmentId = assignmentId;
        ApprovedAt = approvedAt;
        RejectionReason = null;
    }

    public void Reject(string? reason)
    {
        if (Status != OaAssetRequestStatus.Submitted) throw new InvalidOperationException("只有已提交的资产申请可以驳回。");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("资产申请驳回原因不能为空。", nameof(reason));
        Status = OaAssetRequestStatus.Rejected;
        RejectionReason = reason.Trim();
        AssignmentId = null;
    }

    public void Cancel()
    {
        if (Status == OaAssetRequestStatus.Draft) Status = OaAssetRequestStatus.Cancelled;
        else if (Status == OaAssetRequestStatus.Submitted) Status = OaAssetRequestStatus.Withdrawn;
        else throw new InvalidOperationException("当前状态不能撤回资产申请。");
    }

    public void SetStatusForRecovery(OaAssetRequestStatus status, Guid? assignmentId, string? rejectionReason, DateTime? approvedAt, DateTime? submittedAt)
    {
        Status = status;
        AssignmentId = assignmentId;
        RejectionReason = string.IsNullOrWhiteSpace(rejectionReason) ? null : rejectionReason;
        ApprovedAt = approvedAt;
        SubmittedAt = submittedAt;
    }

    private static string Required(string? value, string label) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{label}不能为空。") : value.Trim();
}
