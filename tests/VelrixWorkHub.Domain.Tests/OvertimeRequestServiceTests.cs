using VelrixWorkHub.Application.Leave;
using VelrixWorkHub.Application.Overtime;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class OvertimeRequestServiceTests
{
    [Fact]
    public void CreateAndSubmit_TracksDurationAndBlocksApprovedLeaveOverlap()
    {
        var leaveRepository = new LeaveRepository();
        var leaveService = new LeaveRequestService(leaveRepository);
        var repository = new OvertimeRepository();
        var service = new OvertimeRequestService(repository, leaveService);
        var userId = Guid.CreateVersion7();
        var start = new DateTime(2026, 7, 22, 18, 0, 0);
        var leave = leaveService.Create(userId, OaLeaveType.Annual, start, start.AddHours(3), "休假", null);
        leaveService.Submit(leave, userId);
        leave.Approve();
        leaveRepository.Update(leave);
        var request = service.Create(userId, start, start.AddHours(2), "版本发布支持", "{\"project\":\"A\"}");

        Assert.Equal(2m, request.DurationHours);
        Assert.Throws<InvalidOperationException>(() => service.Submit(request, userId));
        Assert.Equal(OaOvertimeRequestStatus.Draft, request.Status);
    }

    [Fact]
    public void EditRejectResubmitAndCancel_EnforceOwnerAndState()
    {
        var repository = new OvertimeRepository();
        var service = new OvertimeRequestService(repository);
        var userId = Guid.CreateVersion7();
        var otherUserId = Guid.CreateVersion7();
        var start = DateTime.Today.AddDays(1).AddHours(18);
        var request = service.Create(userId, start, start.AddHours(2), "上线支持", null);

        Assert.Throws<UnauthorizedAccessException>(() => service.Edit(request, otherUserId, start, start.AddHours(3), "越权", null));
        service.Submit(request, userId);
        service.ApplyRejection(request, "请补充安排说明");
        service.Edit(request, userId, start, start.AddHours(3), "已补充值班安排", "{\"shift\":\"night\"}");
        service.Submit(request, userId);
        service.Cancel(request, userId, "alice");

        Assert.Equal(OaOvertimeRequestStatus.Cancelled, request.Status);
        Assert.Equal(3m, request.DurationHours);
        Assert.Equal("已补充值班安排", request.Reason);
    }

    [Fact]
    public void Domain_ValidatesTimesJsonAndApprovalTransition()
    {
        var userId = Guid.CreateVersion7();
        Assert.Throws<ArgumentException>(() => new OaOvertimeRequest(userId, DateTime.Today.AddHours(20), DateTime.Today.AddHours(18), "无效", null, DateTime.Now));
        Assert.Throws<ArgumentException>(() => new OaOvertimeRequest(userId, DateTime.Today.AddHours(18), DateTime.Today.AddHours(20), "", null, DateTime.Now));
        Assert.Throws<ArgumentException>(() => new OaOvertimeRequest(userId, DateTime.Today.AddHours(18), DateTime.Today.AddHours(20), "有效", "[]", DateTime.Now));

        var request = new OaOvertimeRequest(userId, DateTime.Today.AddHours(18), DateTime.Today.AddHours(20), "发布支持", null, DateTime.Now);
        request.Submit(DateTime.Now);
        request.Approve();

        Assert.Equal(OaOvertimeRequestStatus.Approved, request.Status);
        Assert.Throws<InvalidOperationException>(() => request.Cancel());
    }

    private sealed class OvertimeRepository : IOaOvertimeRequestRepository
    {
        private readonly List<OaOvertimeRequest> items = [];
        public IReadOnlyList<OaOvertimeRequest> List(Guid? userId = null) => userId is Guid id ? items.Where(item => item.UserId == id).ToArray() : items;
        public OaOvertimeRequest? Get(Guid id) => items.FirstOrDefault(item => item.Id == id);
        public void Add(OaOvertimeRequest request) => items.Add(request);
        public void Update(OaOvertimeRequest request) { }
    }

    private sealed class LeaveRepository : IOaLeaveRequestRepository
    {
        private readonly List<OaLeaveRequest> items = [];
        public IReadOnlyList<OaLeaveRequest> List(Guid? userId = null) => userId is Guid id ? items.Where(item => item.UserId == id).ToArray() : items;
        public OaLeaveRequest? Get(Guid id) => items.FirstOrDefault(item => item.Id == id);
        public void Add(OaLeaveRequest request) => items.Add(request);
        public void Update(OaLeaveRequest request) { }
    }
}
