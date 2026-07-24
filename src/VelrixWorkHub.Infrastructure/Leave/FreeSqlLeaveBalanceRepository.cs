using FreeSql;
using VelrixWorkHub.Application.Leave;
using VelrixWorkHub.Domain;
using BalanceDomain = VelrixWorkHub.Domain.OaLeaveBalance;
using ReservationDomain = VelrixWorkHub.Domain.OaLeaveBalanceReservation;

namespace VelrixWorkHub.Infrastructure.Leave;

public sealed class FreeSqlLeaveBalanceRepository(IFreeSql fsql) : IOaLeaveBalanceRepository, IOaLeaveBalanceReservationRepository
{
    public IReadOnlyList<BalanceDomain> List(Guid? userId = null, int? year = null)
    {
        var query = fsql.Select<OaLeaveBalanceRecord>();
        if (userId is Guid id) query = query.Where(item => item.UserId == id);
        if (year is int selectedYear) query = query.Where(item => item.Year == selectedYear);
        return query.OrderByDescending(item => item.Year).ToList().Select(ToDomain).ToArray();
    }

    public BalanceDomain? Get(Guid userId, int year, OaLeaveType leaveType) =>
        fsql.Select<OaLeaveBalanceRecord>()
            .Where(item => item.UserId == userId && item.Year == year && item.LeaveType == leaveType)
            .ToList().Select(ToDomain).FirstOrDefault();

    public BalanceDomain? Get(Guid id) =>
        fsql.Select<OaLeaveBalanceRecord>().Where(item => item.Id == id).ToList().Select(ToDomain).FirstOrDefault();

    public void Add(BalanceDomain balance) => fsql.Insert(ToRecord(balance)).ExecuteAffrows();

    public void Update(BalanceDomain balance)
    {
        var rows = fsql.Update<OaLeaveBalanceRecord>()
            .Set(item => item.EntitledHours, balance.EntitledHours)
            .Set(item => item.ReservedHours, balance.ReservedHours)
            .Set(item => item.UsedHours, balance.UsedHours)
            .Set(item => item.OtherInfo, balance.OtherInfo)
            .Set(item => item.UpdatedAt, balance.UpdatedAt)
            .Where(item => item.Id == balance.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("请假额度不存在或已被删除。");
    }

    public IReadOnlyList<ReservationDomain> List(Guid? balanceId = null)
    {
        var query = fsql.Select<OaLeaveBalanceReservationRecord>();
        if (balanceId is Guid id) query = query.Where(item => item.BalanceId == id);
        return query.OrderByDescending(item => item.CreatedAt).ToList().Select(ToDomain).ToArray();
    }

    public ReservationDomain? GetByRequest(Guid requestId) =>
        fsql.Select<OaLeaveBalanceReservationRecord>().Where(item => item.RequestId == requestId).ToList().Select(ToDomain).FirstOrDefault();

    public void Add(ReservationDomain reservation) => fsql.Insert(ToRecord(reservation)).ExecuteAffrows();

    public void Update(ReservationDomain reservation)
    {
        var rows = fsql.Update<OaLeaveBalanceReservationRecord>()
            .Set(item => item.BalanceId, reservation.BalanceId)
            .Set(item => item.Hours, reservation.Hours)
            .Set(item => item.Status, reservation.Status)
            .Set(item => item.ReleasedAt, reservation.ReleasedAt)
            .Where(item => item.Id == reservation.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("请假额度占用不存在或已被删除。");
    }

    private static BalanceDomain ToDomain(OaLeaveBalanceRecord item)
    {
        var balance = new BalanceDomain(item.UserId, item.Year, item.LeaveType, item.EntitledHours, item.OtherInfo, item.CreatedAt) { Id = item.Id };
        if (item.ReservedHours + item.UsedHours > 0) balance.Reserve(item.ReservedHours + item.UsedHours);
        if (item.UsedHours > 0) balance.Consume(item.UsedHours);
        return balance;
    }

    private static ReservationDomain ToDomain(OaLeaveBalanceReservationRecord item)
    {
        var reservation = new ReservationDomain(item.BalanceId, item.RequestId, item.Hours, item.CreatedAt) { Id = item.Id };
        if (item.Status == OaLeaveBalanceReservationStatus.Consumed) reservation.Consume();
        else if (item.Status == OaLeaveBalanceReservationStatus.Released) reservation.Release(item.ReleasedAt);
        return reservation;
    }

    private static OaLeaveBalanceRecord ToRecord(BalanceDomain item) => new()
    {
        Id = item.Id, UserId = item.UserId, Year = item.Year, LeaveType = item.LeaveType,
        EntitledHours = item.EntitledHours, ReservedHours = item.ReservedHours, UsedHours = item.UsedHours,
        OtherInfo = item.OtherInfo, CreatedAt = item.CreatedAt, UpdatedAt = item.UpdatedAt
    };

    private static OaLeaveBalanceReservationRecord ToRecord(ReservationDomain item) => new()
    {
        Id = item.Id, BalanceId = item.BalanceId, RequestId = item.RequestId, Hours = item.Hours,
        Status = item.Status, CreatedAt = item.CreatedAt, ReleasedAt = item.ReleasedAt
    };
}
