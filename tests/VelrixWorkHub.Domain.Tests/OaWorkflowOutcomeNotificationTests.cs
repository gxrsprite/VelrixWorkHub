using VelrixWorkHub.Application.Employees;
using VelrixWorkHub.Application.Leave;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class OaWorkflowOutcomeNotificationTests
{
    [Fact]
    public void OutcomeNotificationUsesEnabledApplicantAndStableWorkflowDedupe()
    {
        var userId = Guid.CreateVersion7();
        var notifications = new NotificationRepository();
        var service = CreateService(new EmployeeDirectoryEntry(userId, "Applicant", "申请人", null, null, true, null, null), notifications);
        var workflowId = Guid.CreateVersion7();
        var businessId = Guid.CreateVersion7();

        service.Publish(userId, "OaLeaveRequest", businessId, "请假申请", "2026-07-22", "/Oa/Leave", workflowId, "Approved");
        service.Publish(userId, "OaLeaveRequest", businessId, "请假申请", "2026-07-22", "/Oa/Leave", workflowId, "Approved");

        var notification = Assert.Single(notifications.Items);
        Assert.Equal("applicant", notification.Recipient);
        Assert.Equal("请假申请已批准", notification.Title);
        Assert.Equal("oa-workflow-outcome:OaLeaveRequest:" + businessId + ":" + workflowId + ":Approved", notification.DedupeKey);
    }

    [Fact]
    public void LeaveWorkflowHandlerPublishesRejectionReasonAndSkipsDisabledApplicant()
    {
        var userId = Guid.CreateVersion7();
        var request = new OaLeaveRequest(userId, OaLeaveType.Personal, DateTime.Today.AddHours(9), DateTime.Today.AddHours(17), "办理事务", "{}", DateTime.Now);
        request.Submit(DateTime.Now);
        var repository = new LeaveRepository(request);
        var notifications = new NotificationRepository();
        var handler = new LeaveRequestWorkflowActionHandler(repository, outcomeNotifications: CreateService(
            new EmployeeDirectoryEntry(userId, "Applicant", "申请人", null, null, true, null, null), notifications));
        var instance = CreateInstance(request.Id);
        var rejected = new WorkflowActionDefinition(WorkflowActionType.SetField, nameof(OaLeaveRequest.Status), nameof(OaLeaveRequestStatus.Rejected));

        handler.Execute(new WorkflowActionContext(instance, WorkflowActionTrigger.Rejected, "时间冲突"), rejected);

        var notification = Assert.Single(notifications.Items);
        Assert.Equal("请假申请已驳回", notification.Title);
        Assert.Contains("时间冲突", notification.Content);

        var disabledNotifications = new NotificationRepository();
        var disabledRequest = CreateSubmittedRequest(userId);
        var disabledHandler = new LeaveRequestWorkflowActionHandler(new LeaveRepository(disabledRequest),
            outcomeNotifications: CreateService(new EmployeeDirectoryEntry(userId, "Applicant", "申请人", null, null, false, null, null), disabledNotifications));
        disabledHandler.Execute(new WorkflowActionContext(CreateInstance(disabledRequest.Id), WorkflowActionTrigger.Rejected, "已停用"), rejected);
        Assert.Empty(disabledNotifications.Items);
    }

    private static OaLeaveRequest CreateSubmittedRequest(Guid userId)
    {
        var request = new OaLeaveRequest(userId, OaLeaveType.Other, DateTime.Today.AddHours(9), DateTime.Today.AddHours(10), "其他", "{}", DateTime.Now);
        request.Submit(DateTime.Now);
        return request;
    }

    private static OaWorkflowOutcomeNotificationService CreateService(EmployeeDirectoryEntry entry, NotificationRepository notifications)
        => new(new EmployeeDirectoryService(new DirectoryRepository(entry)), new NotificationService(notifications));

    private static WorkflowInstance CreateInstance(Guid businessId)
    {
        var definition = new WorkflowDefinition(WorkflowBindingCodes.LeaveApproval, "请假审批");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, end.Id);
        definition.Publish();
        return WorkflowInstance.Start(definition, nameof(OaLeaveRequest), businessId, DateTime.Now);
    }

    private sealed class DirectoryRepository(params EmployeeDirectoryEntry[] seed) : IEmployeeDirectoryRepository
    {
        public IReadOnlyList<EmployeeDirectoryEntry> List() => seed;
        public IReadOnlyList<EmployeeDirectoryOrganization> ListOrganizations() => [];
    }

    private sealed class NotificationRepository : INotificationRepository
    {
        public List<WorkNotification> Items { get; } = [];
        public IReadOnlyList<WorkNotification> List(string recipient, bool unreadOnly = false) => Items.Where(x => x.Recipient.Equals(recipient, StringComparison.OrdinalIgnoreCase) && (!unreadOnly || !x.IsRead)).ToArray();
        public WorkNotification? FindByDedupeKey(string recipient, string dedupeKey) => Items.FirstOrDefault(x => x.Recipient.Equals(recipient, StringComparison.OrdinalIgnoreCase) && x.DedupeKey == dedupeKey);
        public void Add(WorkNotification notification) => Items.Add(notification);
        public bool TryAdd(WorkNotification notification) { if (FindByDedupeKey(notification.Recipient, notification.DedupeKey) is not null) return false; Items.Add(notification); return true; }
        public void Update(WorkNotification notification) { }
        public int Delete(string recipient, IReadOnlyCollection<Guid> notificationIds) => 0;
    }

    private sealed class LeaveRepository(params OaLeaveRequest[] seed) : IOaLeaveRequestRepository
    {
        private readonly List<OaLeaveRequest> items = [.. seed];
        public IReadOnlyList<OaLeaveRequest> List(Guid? userId = null) => userId is Guid id ? items.Where(x => x.UserId == id).ToArray() : items;
        public OaLeaveRequest? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public void Add(OaLeaveRequest request) => items.Add(request);
        public void Update(OaLeaveRequest request) { }
    }
}
