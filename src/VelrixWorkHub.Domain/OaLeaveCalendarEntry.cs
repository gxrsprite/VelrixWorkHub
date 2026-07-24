namespace VelrixWorkHub.Domain;

public sealed class OaLeaveCalendarEntry
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid LeaveRequestId { get; private set; }
    public Guid UserId { get; private set; }
    public OaLeaveType LeaveType { get; private set; }
    public DateTime StartAt { get; private set; }
    public DateTime EndAt { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    public OaLeaveCalendarEntry(Guid leaveRequestId, Guid userId, OaLeaveType leaveType, DateTime startAt, DateTime endAt, string reason, DateTime createdAt)
    {
        if (leaveRequestId == Guid.Empty) throw new ArgumentException("请假申请不能为空。", nameof(leaveRequestId));
        if (userId == Guid.Empty) throw new ArgumentException("员工不能为空。", nameof(userId));
        if (endAt <= startAt) throw new ArgumentException("结束时间必须晚于开始时间。", nameof(endAt));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("请假事由不能为空。", nameof(reason));
        LeaveRequestId = leaveRequestId;
        UserId = userId;
        LeaveType = leaveType;
        StartAt = startAt;
        EndAt = endAt;
        Reason = reason.Trim();
        CreatedAt = createdAt;
    }
}
