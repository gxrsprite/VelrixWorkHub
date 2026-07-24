namespace VelrixWorkHub.Domain;

public enum OaOvertimeRequestStatus
{
    Draft,
    Submitted,
    Approved,
    Rejected,
    Cancelled
}

/// <summary>员工加班申请。批准只确认申请，不直接写入考勤或工时。</summary>
public sealed class OaOvertimeRequest
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid UserId { get; private set; }
    public DateTime StartAt { get; private set; }
    public DateTime EndAt { get; private set; }
    public decimal DurationHours => decimal.Round((decimal)(EndAt - StartAt).TotalHours, 2);
    public string Reason { get; private set; } = string.Empty;
    public string OtherInfo { get; private set; } = "{}";
    public OaOvertimeRequestStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? SubmittedAt { get; private set; }

    public OaOvertimeRequest(Guid userId, DateTime startAt, DateTime endAt, string reason, string? otherInfo, DateTime createdAt)
    {
        if (userId == Guid.Empty) throw new ArgumentException("申请人不能为空。", nameof(userId));
        UserId = userId;
        CreatedAt = createdAt;
        Edit(startAt, endAt, reason, otherInfo);
        Status = OaOvertimeRequestStatus.Draft;
    }

    public void Edit(DateTime startAt, DateTime endAt, string reason, string? otherInfo)
    {
        if (endAt <= startAt) throw new ArgumentException("结束时间必须晚于开始时间。", nameof(endAt));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("加班事由不能为空。", nameof(reason));
        StartAt = startAt;
        EndAt = endAt;
        Reason = reason.Trim();
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public void Submit(DateTime submittedAt)
    {
        if (Status is not (OaOvertimeRequestStatus.Draft or OaOvertimeRequestStatus.Rejected)) throw new InvalidOperationException("只有草稿或已驳回加班申请才能提交。");
        Status = OaOvertimeRequestStatus.Submitted;
        RejectionReason = null;
        SubmittedAt = submittedAt;
    }

    public void Cancel()
    {
        if (Status is not (OaOvertimeRequestStatus.Draft or OaOvertimeRequestStatus.Submitted)) throw new InvalidOperationException("当前状态不能撤回加班申请。");
        Status = OaOvertimeRequestStatus.Cancelled;
    }

    public void Approve()
    {
        if (Status != OaOvertimeRequestStatus.Submitted) throw new InvalidOperationException("只有已提交的加班申请才能批准。");
        Status = OaOvertimeRequestStatus.Approved;
    }

    public void Reject(string? reason = null)
    {
        if (Status != OaOvertimeRequestStatus.Submitted) throw new InvalidOperationException("只有已提交的加班申请才能驳回。");
        Status = OaOvertimeRequestStatus.Rejected;
        RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    public bool Overlaps(DateTime startAt, DateTime endAt) => StartAt < endAt && startAt < EndAt;

    /// <summary>仅供事务失败恢复或持久化重建使用。</summary>
    public void SetStatus(OaOvertimeRequestStatus status) => Status = status;
}
