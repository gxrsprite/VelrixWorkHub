using VelrixWorkHub.Application.Leave;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class LeaveRequestServiceTests
{
    [Fact]
    public void CreateAndSubmit_TracksDurationAndPreventsOverlap()
    {
        var repository = new LeaveRepository();
        var service = new LeaveRequestService(repository);
        var userId = Guid.CreateVersion7();
        var start = new DateTime(2026, 7, 21, 9, 0, 0);
        var first = service.Create(userId, OaLeaveType.Annual, start, start.AddHours(8), "家庭安排", null);

        Assert.Equal(8m, first.DurationHours);
        service.Submit(first, userId);
        Assert.Equal(OaLeaveRequestStatus.Submitted, first.Status);
        var overlappingDraft = service.Create(userId, OaLeaveType.Personal, start.AddHours(2), start.AddHours(10), "重复时间", null);
        Assert.Throws<InvalidOperationException>(() => service.Submit(overlappingDraft, userId));
    }

    [Fact]
    public void EditAndCancel_AreOwnerOnlyAndSubmittedCanBeWithdrawn()
    {
        var repository = new LeaveRepository();
        var service = new LeaveRequestService(repository);
        var userId = Guid.CreateVersion7();
        var otherUserId = Guid.CreateVersion7();
        var request = service.Create(userId, OaLeaveType.Sick, DateTime.Now.AddDays(1), DateTime.Now.AddDays(1).AddHours(4), "就医", null);

        Assert.Throws<UnauthorizedAccessException>(() => service.Edit(request, otherUserId, OaLeaveType.Sick, request.StartAt, request.EndAt, "越权", null));
        service.Edit(request, userId, OaLeaveType.Sick, request.StartAt, request.EndAt.AddHours(1), "复诊", "{\"doctor\":\"A\"}");
        service.Submit(request, userId);
        service.Cancel(request, userId);

        Assert.Equal(OaLeaveRequestStatus.Cancelled, request.Status);
        Assert.Equal("复诊", request.Reason);
        Assert.Throws<InvalidOperationException>(() => service.Edit(request, userId, OaLeaveType.Sick, request.StartAt, request.EndAt, "不能编辑", null));
    }

    [Fact]
    public void Domain_ValidatesRequiredFieldsAndStatusTransitions()
    {
        var userId = Guid.CreateVersion7();
        Assert.Throws<ArgumentException>(() => new OaLeaveRequest(userId, OaLeaveType.Other, DateTime.Today.AddHours(10), DateTime.Today.AddHours(9), "无效", null, DateTime.Now));
        Assert.Throws<ArgumentException>(() => new OaLeaveRequest(userId, OaLeaveType.Other, DateTime.Today.AddHours(9), DateTime.Today.AddHours(10), " ", "[]", DateTime.Now));

        var request = new OaLeaveRequest(userId, OaLeaveType.Other, DateTime.Today.AddHours(9), DateTime.Today.AddHours(10), "其他事由", null, DateTime.Now);
        request.Submit(DateTime.Now);
        request.Reject();
        Assert.Equal(OaLeaveRequestStatus.Rejected, request.Status);
        Assert.Throws<InvalidOperationException>(() => request.Cancel());
    }

    [Fact]
    public void RejectedRequestCanBeEditedAndResubmittedWithReasonCleared()
    {
        var repository = new LeaveRepository();
        var service = new LeaveRequestService(repository);
        var userId = Guid.CreateVersion7();
        var request = service.Create(userId, OaLeaveType.Personal, DateTime.Today.AddDays(1).AddHours(9), DateTime.Today.AddDays(1).AddHours(12), "临时安排", null);

        service.Submit(request, userId);
        request.Reject("请补充证明");
        repository.Update(request);
        service.Edit(request, userId, OaLeaveType.Sick, request.StartAt, request.EndAt, "就医证明已补充", "{\"source\":\"hr\"}");
        service.Submit(request, userId);

        Assert.Equal(OaLeaveRequestStatus.Submitted, request.Status);
        Assert.Null(request.RejectionReason);
        Assert.Equal("就医证明已补充", request.Reason);
    }

    [Fact]
    public void RejectedRequestStillCannotResubmitOverlappingApprovedPeriod()
    {
        var repository = new LeaveRepository();
        var service = new LeaveRequestService(repository);
        var userId = Guid.CreateVersion7();
        var start = DateTime.Today.AddDays(2).AddHours(9);
        var approved = service.Create(userId, OaLeaveType.Annual, start, start.AddHours(4), "已批准安排", null);
        service.Submit(approved, userId);
        approved.Approve();
        repository.Update(approved);

        var rejected = service.Create(userId, OaLeaveType.Personal, start.AddDays(1), start.AddDays(1).AddHours(2), "待补充材料", null);
        service.Submit(rejected, userId);
        rejected.Reject("材料不足");
        repository.Update(rejected);
        service.Edit(rejected, userId, OaLeaveType.Personal, start.AddHours(1), start.AddHours(3), "补充后仍冲突", null);

        Assert.Throws<InvalidOperationException>(() => service.Submit(rejected, userId));
        Assert.Equal(OaLeaveRequestStatus.Rejected, rejected.Status);
    }

    private sealed class LeaveRepository : IOaLeaveRequestRepository
    {
        private readonly List<OaLeaveRequest> requests = [];
        public IReadOnlyList<OaLeaveRequest> List(Guid? userId = null) => userId is Guid id ? requests.Where(item => item.UserId == id).ToArray() : requests;
        public OaLeaveRequest? Get(Guid id) => requests.FirstOrDefault(item => item.Id == id);
        public void Add(OaLeaveRequest request) => requests.Add(request);
        public void Update(OaLeaveRequest request) { }
    }
}
