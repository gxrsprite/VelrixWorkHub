using VelrixWorkHub.Application.Leave;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class LeaveCalendarServiceTests
{
    [Fact]
    public void ApprovalCreatesExactlyOnePersonalCalendarEntry()
    {
        var requests = new LeaveRepository();
        var entries = new CalendarRepository();
        var service = new LeaveRequestService(requests, calendar: new LeaveCalendarService(entries));
        var userId = Guid.CreateVersion7();
        var start = new DateTime(2026, 8, 3, 9, 0, 0);
        var request = service.Create(userId, OaLeaveType.Annual, start, start.AddHours(8), "家庭安排", "{}");
        service.Submit(request, userId);

        service.ApplyApproval(request);
        service.ApplyApproval(request);

        var entry = Assert.Single(entries.List(userId));
        Assert.Equal(request.Id, entry.LeaveRequestId);
        Assert.Equal(OaLeaveType.Annual, entry.LeaveType);
        Assert.Equal(request.StartAt, entry.StartAt);
        Assert.Equal(request.EndAt, entry.EndAt);
        Assert.Equal("家庭安排", entry.Reason);
    }

    [Fact]
    public void CalendarEntriesAreUserScopedAndCannotBeCreatedForUnapprovedRequests()
    {
        var entries = new CalendarRepository();
        var calendar = new LeaveCalendarService(entries);
        var firstUser = Guid.CreateVersion7();
        var secondUser = Guid.CreateVersion7();
        var request = new OaLeaveRequest(firstUser, OaLeaveType.Sick, DateTime.Today.AddDays(2).AddHours(9), DateTime.Today.AddDays(2).AddHours(13), "就医", "{}", DateTime.Now);

        Assert.Throws<InvalidOperationException>(() => calendar.CreateForApproval(request));
        request.Submit(DateTime.Now);
        calendar.CreateForApproval(request);

        Assert.Single(calendar.ListMine(firstUser));
        Assert.Empty(calendar.ListMine(secondUser));
    }

    private sealed class LeaveRepository : IOaLeaveRequestRepository
    {
        private readonly List<OaLeaveRequest> items = [];
        public IReadOnlyList<OaLeaveRequest> List(Guid? userId = null) => items.Where(x => userId is null || x.UserId == userId).ToArray();
        public OaLeaveRequest? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public void Add(OaLeaveRequest request) => items.Add(request);
        public void Update(OaLeaveRequest request) { }
    }

    private sealed class CalendarRepository : IOaLeaveCalendarEntryRepository
    {
        private readonly List<OaLeaveCalendarEntry> items = [];
        public IReadOnlyList<OaLeaveCalendarEntry> List(Guid userId) => items.Where(x => x.UserId == userId).ToArray();
        public OaLeaveCalendarEntry? GetByLeaveRequest(Guid leaveRequestId) => items.FirstOrDefault(x => x.LeaveRequestId == leaveRequestId);
        public void Add(OaLeaveCalendarEntry entry) => items.Add(entry);
    }
}
