using VelrixWorkHub.Application.Leave;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class LeaveBalanceServiceTests
{
    [Fact]
    public void AnnualLeave_ReservesReleasesAndConsumesBalance()
    {
        var userId = Guid.CreateVersion7();
        var balanceRepository = new BalanceRepository();
        var reservationRepository = new ReservationRepository();
        var balances = new LeaveBalanceService(balanceRepository, reservationRepository);
        balances.Save(userId, 2026, OaLeaveType.Annual, 8m, "{\"source\":\"hr\"}", "admin", true);
        var requests = new LeaveRepository();
        var service = new LeaveRequestService(requests, balances: balances);
        var request = service.Create(userId, OaLeaveType.Annual, new DateTime(2026, 8, 3, 9, 0, 0), new DateTime(2026, 8, 3, 17, 0, 0), "年假安排", null);

        service.Submit(request, userId);
        var balance = balanceRepository.Get(userId, 2026, OaLeaveType.Annual)!;
        Assert.Equal(8m, balance.ReservedHours);
        Assert.Equal(0m, balance.AvailableHours);

        service.ApplyRejection(request, "请补充交接安排");
        balance = balanceRepository.Get(userId, 2026, OaLeaveType.Annual)!;
        Assert.Equal(0m, balance.ReservedHours);
        Assert.Equal(8m, balance.AvailableHours);

        service.Edit(request, userId, OaLeaveType.Annual, request.StartAt, request.EndAt, "交接已补充", null);
        service.Submit(request, userId);
        service.ApplyApproval(request);

        balance = balanceRepository.Get(userId, 2026, OaLeaveType.Annual)!;
        Assert.Equal(0m, balance.ReservedHours);
        Assert.Equal(8m, balance.UsedHours);
        Assert.Equal(0m, balance.AvailableHours);
        Assert.Equal(OaLeaveBalanceReservationStatus.Consumed, reservationRepository.GetByRequest(request.Id)!.Status);
    }

    [Fact]
    public void AnnualLeave_RequiresConfiguredBalanceAndRejectsOverQuota()
    {
        var userId = Guid.CreateVersion7();
        var balanceRepository = new BalanceRepository();
        var reservationRepository = new ReservationRepository();
        var balances = new LeaveBalanceService(balanceRepository, reservationRepository);
        var requests = new LeaveRepository();
        var service = new LeaveRequestService(requests, balances: balances);
        var request = service.Create(userId, OaLeaveType.Annual, new DateTime(2026, 8, 3, 9, 0, 0), new DateTime(2026, 8, 3, 17, 0, 0), "年假安排", null);

        Assert.Throws<InvalidOperationException>(() => service.Submit(request, userId));
        Assert.Equal(OaLeaveRequestStatus.Draft, request.Status);

        balances.Save(userId, 2026, OaLeaveType.Annual, 4m, null, "admin", true);
        Assert.Throws<InvalidOperationException>(() => service.Submit(request, userId));
        Assert.Equal(OaLeaveRequestStatus.Draft, request.Status);
        Assert.Equal(4m, balanceRepository.Get(userId, 2026, OaLeaveType.Annual)!.AvailableHours);
    }

    [Fact]
    public void NonQuotaLeave_DoesNotRequireBalanceAndQuotaCannotBeChangedBelowUsage()
    {
        var userId = Guid.CreateVersion7();
        var balanceRepository = new BalanceRepository();
        var reservationRepository = new ReservationRepository();
        var balances = new LeaveBalanceService(balanceRepository, reservationRepository);
        var requests = new LeaveRepository();
        var service = new LeaveRequestService(requests, balances: balances);
        var sick = service.Create(userId, OaLeaveType.Sick, DateTime.Today.AddDays(1).AddHours(9), DateTime.Today.AddDays(1).AddHours(12), "就医", null);

        service.Submit(sick, userId);
        Assert.Equal(OaLeaveRequestStatus.Submitted, sick.Status);
        var annual = balances.Save(userId, 2026, OaLeaveType.Annual, 4m, null, "admin", true);
        annual.Reserve(2m);
        Assert.Throws<InvalidOperationException>(() => balances.Save(userId, 2026, OaLeaveType.Annual, 1m, null, "admin", true));
        Assert.Throws<InvalidOperationException>(() => balances.Save(userId, 2026, OaLeaveType.Sick, 8m, null, "admin", true));
    }

    private sealed class BalanceRepository : IOaLeaveBalanceRepository
    {
        private readonly List<OaLeaveBalance> items = [];
        public IReadOnlyList<OaLeaveBalance> List(Guid? userId = null, int? year = null) => items.Where(item => (!userId.HasValue || item.UserId == userId) && (!year.HasValue || item.Year == year)).ToArray();
        public OaLeaveBalance? Get(Guid userId, int year, OaLeaveType leaveType) => items.FirstOrDefault(item => item.UserId == userId && item.Year == year && item.LeaveType == leaveType);
        public OaLeaveBalance? Get(Guid id) => items.FirstOrDefault(item => item.Id == id);
        public void Add(OaLeaveBalance balance) => items.Add(balance);
        public void Update(OaLeaveBalance balance) { }
    }

    private sealed class ReservationRepository : IOaLeaveBalanceReservationRepository
    {
        private readonly List<OaLeaveBalanceReservation> items = [];
        public IReadOnlyList<OaLeaveBalanceReservation> List(Guid? balanceId = null) => items.Where(item => !balanceId.HasValue || item.BalanceId == balanceId).ToArray();
        public OaLeaveBalanceReservation? GetByRequest(Guid requestId) => items.FirstOrDefault(item => item.RequestId == requestId);
        public void Add(OaLeaveBalanceReservation reservation) => items.Add(reservation);
        public void Update(OaLeaveBalanceReservation reservation) { }
    }

    private sealed class LeaveRepository : IOaLeaveRequestRepository
    {
        private readonly List<OaLeaveRequest> items = [];
        public IReadOnlyList<OaLeaveRequest> List(Guid? userId = null) => items.Where(item => !userId.HasValue || item.UserId == userId).ToArray();
        public OaLeaveRequest? Get(Guid id) => items.FirstOrDefault(item => item.Id == id);
        public void Add(OaLeaveRequest request) => items.Add(request);
        public void Update(OaLeaveRequest request) { }
    }
}
