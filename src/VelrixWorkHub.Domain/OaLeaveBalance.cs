namespace VelrixWorkHub.Domain;

public enum OaLeaveBalanceReservationStatus
{
    Reserved,
    Consumed,
    Released
}

public sealed class OaLeaveBalance
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid UserId { get; private set; }
    public int Year { get; private set; }
    public OaLeaveType LeaveType { get; private set; }
    public decimal EntitledHours { get; private set; }
    public decimal ReservedHours { get; private set; }
    public decimal UsedHours { get; private set; }
    public decimal AvailableHours => decimal.Round(Math.Max(0m, EntitledHours - ReservedHours - UsedHours), 2);
    public string OtherInfo { get; private set; } = "{}";
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public OaLeaveBalance(Guid userId, int year, OaLeaveType leaveType, decimal entitledHours, string? otherInfo, DateTime createdAt)
    {
        if (userId == Guid.Empty) throw new ArgumentException("员工不能为空。", nameof(userId));
        if (year < 2000 || year > 2200) throw new ArgumentOutOfRangeException(nameof(year), "额度年度不合法。");
        EnsureQuotaType(leaveType);
        UserId = userId;
        Year = year;
        LeaveType = leaveType;
        CreatedAt = createdAt;
        Edit(entitledHours, otherInfo, createdAt);
    }

    public void Edit(decimal entitledHours, string? otherInfo, DateTime updatedAt)
    {
        if (entitledHours < 0) throw new ArgumentOutOfRangeException(nameof(entitledHours), "额度不能为负数。");
        if (entitledHours < ReservedHours + UsedHours)
            throw new InvalidOperationException("新额度不能低于已占用和已使用时长。");
        EntitledHours = decimal.Round(entitledHours, 2);
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
        UpdatedAt = updatedAt;
    }

    public void Reserve(decimal hours)
    {
        EnsureHours(hours);
        if (AvailableHours < hours) throw new InvalidOperationException("请假额度不足，不能提交申请。");
        ReservedHours = decimal.Round(ReservedHours + hours, 2);
    }

    public void Grant(decimal hours, DateTime updatedAt)
    {
        EnsureHours(hours);
        EntitledHours = decimal.Round(EntitledHours + hours, 2);
        UpdatedAt = updatedAt;
    }

    public void Release(decimal hours)
    {
        EnsureHours(hours);
        if (ReservedHours < hours) throw new InvalidOperationException("请假额度占用记录不一致，不能释放。");
        ReservedHours = decimal.Round(ReservedHours - hours, 2);
    }

    public void Consume(decimal hours)
    {
        EnsureHours(hours);
        if (ReservedHours < hours) throw new InvalidOperationException("请假额度未被占用，不能转为已使用。");
        ReservedHours = decimal.Round(ReservedHours - hours, 2);
        UsedHours = decimal.Round(UsedHours + hours, 2);
    }

    private static void EnsureQuotaType(OaLeaveType leaveType)
    {
        if (leaveType is not (OaLeaveType.Annual or OaLeaveType.Compensatory))
            throw new ArgumentException("只有年假或调休支持额度台账。", nameof(leaveType));
    }

    private static void EnsureHours(decimal hours)
    {
        if (hours <= 0) throw new ArgumentOutOfRangeException(nameof(hours), "请假时长必须大于 0。");
    }
}

public sealed class OaLeaveBalanceReservation
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid BalanceId { get; private set; }
    public Guid RequestId { get; private set; }
    public decimal Hours { get; private set; }
    public OaLeaveBalanceReservationStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReleasedAt { get; private set; }

    public OaLeaveBalanceReservation(Guid balanceId, Guid requestId, decimal hours, DateTime createdAt)
    {
        if (balanceId == Guid.Empty) throw new ArgumentException("额度不能为空。", nameof(balanceId));
        if (requestId == Guid.Empty) throw new ArgumentException("请假申请不能为空。", nameof(requestId));
        if (hours <= 0) throw new ArgumentOutOfRangeException(nameof(hours), "请假时长必须大于 0。");
        BalanceId = balanceId;
        RequestId = requestId;
        Hours = decimal.Round(hours, 2);
        CreatedAt = createdAt;
        Status = OaLeaveBalanceReservationStatus.Reserved;
    }

    public void ReserveAgain(decimal hours)
    {
        if (Status != OaLeaveBalanceReservationStatus.Released) throw new InvalidOperationException("当前请假额度占用不能重新激活。");
        if (hours <= 0) throw new ArgumentOutOfRangeException(nameof(hours), "请假时长必须大于 0。");
        Hours = decimal.Round(hours, 2);
        Status = OaLeaveBalanceReservationStatus.Reserved;
        ReleasedAt = null;
    }

    public void Release(DateTime? releasedAt = null)
    {
        if (Status == OaLeaveBalanceReservationStatus.Released) return;
        if (Status != OaLeaveBalanceReservationStatus.Reserved) throw new InvalidOperationException("已使用的请假额度不能释放。");
        Status = OaLeaveBalanceReservationStatus.Released;
        ReleasedAt = releasedAt ?? DateTime.Now;
    }

    public void Consume()
    {
        if (Status == OaLeaveBalanceReservationStatus.Consumed) return;
        if (Status != OaLeaveBalanceReservationStatus.Reserved) throw new InvalidOperationException("当前请假额度占用不能转为已使用。");
        Status = OaLeaveBalanceReservationStatus.Consumed;
    }
}
