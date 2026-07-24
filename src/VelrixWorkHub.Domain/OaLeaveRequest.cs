namespace VelrixWorkHub.Domain;

public enum OaLeaveType
{
    Annual,
    Sick,
    Personal,
    Compensatory,
    Other
}

public enum OaLeaveRequestStatus
{
    Draft,
    Submitted,
    Approved,
    Rejected,
    Cancelled
}

public sealed class OaLeaveRequest
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid UserId { get; private set; }
    public OaLeaveType LeaveType { get; private set; }
    public DateTime StartAt { get; private set; }
    public DateTime EndAt { get; private set; }
    public decimal DurationHours => decimal.Round((decimal)(EndAt - StartAt).TotalHours, 2);
    public string Reason { get; private set; } = string.Empty;
    public string OtherInfo { get; private set; } = "{}";
    public OaLeaveRequestStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? SubmittedAt { get; private set; }

    public OaLeaveRequest(Guid userId, OaLeaveType leaveType, DateTime startAt, DateTime endAt, string reason, string? otherInfo, DateTime createdAt)
    {
        if (userId == Guid.Empty) throw new ArgumentException("申请人不能为空。", nameof(userId));
        UserId = userId;
        CreatedAt = createdAt;
        Edit(leaveType, startAt, endAt, reason, otherInfo);
        Status = OaLeaveRequestStatus.Draft;
    }

    public void Edit(OaLeaveType leaveType, DateTime startAt, DateTime endAt, string reason, string? otherInfo)
    {
        if (endAt <= startAt) throw new ArgumentException("结束时间必须晚于开始时间。", nameof(endAt));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("请假事由不能为空。", nameof(reason));
        LeaveType = leaveType;
        StartAt = startAt;
        EndAt = endAt;
        Reason = reason.Trim();
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public void Submit(DateTime submittedAt)
    {
        if (Status is not (OaLeaveRequestStatus.Draft or OaLeaveRequestStatus.Rejected)) throw new InvalidOperationException("只有草稿或已驳回请假申请才能提交。");
        Status = OaLeaveRequestStatus.Submitted;
        RejectionReason = null;
        SubmittedAt = submittedAt;
    }

    public void Cancel()
    {
        if (Status is not (OaLeaveRequestStatus.Draft or OaLeaveRequestStatus.Submitted))
            throw new InvalidOperationException("当前状态不能撤回请假申请。");
        Status = OaLeaveRequestStatus.Cancelled;
    }

    public void Approve()
    {
        if (Status != OaLeaveRequestStatus.Submitted) throw new InvalidOperationException("只有已提交的请假申请才能批准。");
        Status = OaLeaveRequestStatus.Approved;
    }

    public void Reject(string? reason = null)
    {
        if (Status != OaLeaveRequestStatus.Submitted) throw new InvalidOperationException("只有已提交的请假申请才能驳回。");
        Status = OaLeaveRequestStatus.Rejected;
        RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    public bool Overlaps(DateTime startAt, DateTime endAt) => StartAt < endAt && startAt < EndAt;

    /// <summary>仅供事务失败恢复或持久化重建使用。</summary>
    public void SetStatus(OaLeaveRequestStatus status) => Status = status;
}
