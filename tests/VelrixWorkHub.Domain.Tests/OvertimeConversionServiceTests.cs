using VelrixWorkHub.Application.Leave;
using VelrixWorkHub.Application.Overtime;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class OvertimeConversionServiceTests
{
    [Fact]
    public void CompensatoryConversionGrantsHoursAndPreventsSecondChoice()
    {
        var userId = Guid.CreateVersion7();
        var overtime = Approved(userId, DateTime.Now.AddDays(-2));
        var balances = new BalanceRepository();
        var service = new OvertimeConversionService(new ConversionRepository(), new LeaveBalanceService(balances, new ReservationRepository()));

        var converted = service.Convert(overtime, userId, OaOvertimeConversionType.CompensatoryLeave);

        Assert.Equal(2m, converted.Hours);
        var balance = balances.Get(userId, overtime.EndAt.Year, OaLeaveType.Compensatory);
        Assert.NotNull(balance);
        Assert.Equal(2m, balance!.AvailableHours);
        Assert.Throws<InvalidOperationException>(() => service.Convert(overtime, userId, OaOvertimeConversionType.FinanceManual));
    }

    [Fact]
    public void FinanceConversionOnlyRecordsHoursAndEnforcesEligibility()
    {
        var userId = Guid.CreateVersion7();
        var conversions = new ConversionRepository();
        var service = new OvertimeConversionService(conversions, new LeaveBalanceService(new BalanceRepository(), new ReservationRepository()));
        var overtime = Approved(userId, DateTime.Now.AddDays(-3));

        var converted = service.Convert(overtime, userId, OaOvertimeConversionType.FinanceManual);

        Assert.Equal(OaOvertimeConversionType.FinanceManual, converted.Type);
        Assert.Single(conversions.List(userId));
        Assert.Throws<UnauthorizedAccessException>(() => service.Convert(Approved(userId, DateTime.Now.AddDays(-1)), Guid.CreateVersion7(), OaOvertimeConversionType.FinanceManual));
        Assert.Throws<InvalidOperationException>(() => service.Convert(Approved(userId, DateTime.Now.AddDays(-31)), userId, OaOvertimeConversionType.FinanceManual));
        Assert.Throws<InvalidOperationException>(() => service.Convert(Approved(userId, DateTime.Now.AddHours(1)), userId, OaOvertimeConversionType.FinanceManual));
    }

    [Fact]
    public void FinanceProcessingRequiresPermissionAndRecordsHandler()
    {
        var userId = Guid.CreateVersion7();
        var conversions = new ConversionRepository();
        var service = new OvertimeConversionService(conversions, new LeaveBalanceService(new BalanceRepository(), new ReservationRepository()));
        var converted = service.Convert(Approved(userId, DateTime.Now.AddDays(-1)), userId, OaOvertimeConversionType.FinanceManual);

        Assert.Single(service.ListPendingFinanceProcessing());
        Assert.Throws<UnauthorizedAccessException>(() => service.MarkFinanceProcessed(converted.Id, "finance", "已纳入本月人工核算", false));

        service.MarkFinanceProcessed(converted.Id, "finance", "已纳入本月人工核算", true);

        Assert.Empty(service.ListPendingFinanceProcessing());
        Assert.Equal(OaOvertimeFinanceProcessingStatus.Processed, converted.FinanceProcessingStatus);
        Assert.Equal("finance", converted.FinanceProcessedBy);
        Assert.NotNull(converted.FinanceProcessedAt);
        Assert.Equal("已纳入本月人工核算", converted.FinanceProcessingNote);
        Assert.Throws<InvalidOperationException>(() => service.MarkFinanceProcessed(converted.Id, "finance", null, true));
    }

    private static OaOvertimeRequest Approved(Guid userId, DateTime start)
    {
        var item = new OaOvertimeRequest(userId, start, start.AddHours(2), "上线支持", "{}", start);
        item.Submit(start);
        item.Approve();
        return item;
    }

    private sealed class ConversionRepository : IOaOvertimeConversionRepository
    {
        private readonly List<OaOvertimeConversion> items = [];
        public IReadOnlyList<OaOvertimeConversion> List(Guid? userId = null) => items.Where(x => userId is null || x.UserId == userId).ToArray();
        public OaOvertimeConversion? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public OaOvertimeConversion? GetByOvertimeRequest(Guid overtimeRequestId) => items.FirstOrDefault(x => x.OvertimeRequestId == overtimeRequestId);
        public void Add(OaOvertimeConversion item) => items.Add(item);
        public void Update(OaOvertimeConversion item) { }
    }

    private sealed class BalanceRepository : IOaLeaveBalanceRepository
    {
        private readonly List<OaLeaveBalance> items = [];
        public IReadOnlyList<OaLeaveBalance> List(Guid? userId = null, int? year = null) => items.Where(x => (userId is null || x.UserId == userId) && (year is null || x.Year == year)).ToArray();
        public OaLeaveBalance? Get(Guid userId, int year, OaLeaveType leaveType) => items.FirstOrDefault(x => x.UserId == userId && x.Year == year && x.LeaveType == leaveType);
        public OaLeaveBalance? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public void Add(OaLeaveBalance balance) => items.Add(balance);
        public void Update(OaLeaveBalance balance) { }
    }

    private sealed class ReservationRepository : IOaLeaveBalanceReservationRepository
    {
        public IReadOnlyList<OaLeaveBalanceReservation> List(Guid? balanceId = null) => [];
        public OaLeaveBalanceReservation? GetByRequest(Guid requestId) => null;
        public void Add(OaLeaveBalanceReservation reservation) { }
        public void Update(OaLeaveBalanceReservation reservation) { }
    }
}
