using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Leave;

public interface IOaLeaveBalanceRepository
{
    IReadOnlyList<OaLeaveBalance> List(Guid? userId = null, int? year = null);
    OaLeaveBalance? Get(Guid userId, int year, OaLeaveType leaveType);
    OaLeaveBalance? Get(Guid id);
    void Add(OaLeaveBalance balance);
    void Update(OaLeaveBalance balance);
}

public interface IOaLeaveBalanceReservationRepository
{
    IReadOnlyList<OaLeaveBalanceReservation> List(Guid? balanceId = null);
    OaLeaveBalanceReservation? GetByRequest(Guid requestId);
    void Add(OaLeaveBalanceReservation reservation);
    void Update(OaLeaveBalanceReservation reservation);
}

public sealed class LeaveBalanceService(
    IOaLeaveBalanceRepository balances,
    IOaLeaveBalanceReservationRepository reservations)
{
    public static bool RequiresBalance(OaLeaveType leaveType)
        => leaveType is OaLeaveType.Annual or OaLeaveType.Compensatory;

    public IReadOnlyList<OaLeaveBalance> List(Guid? userId = null, int? year = null)
        => balances.List(userId, year).OrderByDescending(item => item.Year).ThenBy(item => item.UserId).ThenBy(item => item.LeaveType).ToArray();

    public IReadOnlyList<OaLeaveBalanceReservation> ListReservations(Guid balanceId)
        => reservations.List(balanceId).OrderByDescending(item => item.CreatedAt).ToArray();

    public OaLeaveBalance Save(Guid userId, int year, OaLeaveType leaveType, decimal entitledHours, string? otherInfo, string actor, bool canManage)
    {
        if (!canManage) throw new UnauthorizedAccessException("当前用户没有维护请假额度的权限。");
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("操作者不能为空。", nameof(actor));
        if (!RequiresBalance(leaveType)) throw new InvalidOperationException("只有年假或调休支持额度台账。");

        var existing = balances.Get(userId, year, leaveType);
        if (existing is null)
        {
            var created = new OaLeaveBalance(userId, year, leaveType, entitledHours, otherInfo, DateTime.Now);
            balances.Add(created);
            return created;
        }

        existing.Edit(entitledHours, otherInfo, DateTime.Now);
        balances.Update(existing);
        return existing;
    }

    public void ReserveForSubmission(OaLeaveRequest request)
    {
        if (!RequiresBalance(request.LeaveType)) return;
        EnsureSingleYear(request);
        var balance = balances.Get(request.UserId, request.StartAt.Year, request.LeaveType)
            ?? throw new InvalidOperationException($"未配置 {request.StartAt.Year} 年{LeaveTypeLabel(request.LeaveType)}额度，不能提交请假申请。");
        var existing = reservations.GetByRequest(request.Id);
        if (existing?.Status == OaLeaveBalanceReservationStatus.Consumed)
            throw new InvalidOperationException("该请假申请额度已结算，不能再次提交。");
        if (existing?.Status == OaLeaveBalanceReservationStatus.Reserved)
        {
            if (existing.Hours != request.DurationHours) throw new InvalidOperationException("请假时长已变化，请先释放原额度占用后再提交。");
            return;
        }

        balance.Reserve(request.DurationHours);
        if (existing?.Status == OaLeaveBalanceReservationStatus.Released)
        {
            existing.ReserveAgain(request.DurationHours);
            balances.Update(balance);
            reservations.Update(existing);
            return;
        }

        balances.Update(balance);
        reservations.Add(new OaLeaveBalanceReservation(balance.Id, request.Id, request.DurationHours, DateTime.Now));
    }

    public void ReleaseForRequest(OaLeaveRequest request)
    {
        var reservation = reservations.GetByRequest(request.Id);
        if (reservation is null || reservation.Status != OaLeaveBalanceReservationStatus.Reserved) return;
        var balance = balances.Get(reservation.BalanceId) ?? throw new InvalidOperationException("请假额度不存在，不能释放申请占用。");
        balance.Release(reservation.Hours);
        reservation.Release();
        balances.Update(balance);
        reservations.Update(reservation);
    }

    public void ConsumeForApproval(OaLeaveRequest request)
    {
        if (!RequiresBalance(request.LeaveType)) return;
        var reservation = reservations.GetByRequest(request.Id) ?? throw new InvalidOperationException("请假申请尚未占用额度，不能批准。");
        if (reservation.Status == OaLeaveBalanceReservationStatus.Consumed) return;
        if (reservation.Status != OaLeaveBalanceReservationStatus.Reserved) throw new InvalidOperationException("请假额度占用已释放，不能批准申请。");
        var balance = balances.Get(reservation.BalanceId) ?? throw new InvalidOperationException("请假额度不存在，不能批准申请。");
        balance.Consume(reservation.Hours);
        reservation.Consume();
        balances.Update(balance);
        reservations.Update(reservation);
    }

    public void GrantOvertimeCompensatory(Guid userId, int year, decimal hours)
    {
        var balance = balances.Get(userId, year, OaLeaveType.Compensatory);
        if (balance is null)
        {
            balances.Add(new OaLeaveBalance(userId, year, OaLeaveType.Compensatory, hours, "{\"source\":\"approved-overtime\"}", DateTime.Now));
            return;
        }
        balance.Grant(hours, DateTime.Now);
        balances.Update(balance);
    }

    private static void EnsureSingleYear(OaLeaveRequest request)
    {
        if (request.StartAt.Year != request.EndAt.AddTicks(-1).Year)
            throw new InvalidOperationException("年假或调休申请不能跨年度，请拆分为多条申请。");
    }

    private static string LeaveTypeLabel(OaLeaveType type) => type == OaLeaveType.Annual ? "年假" : "调休";
}
