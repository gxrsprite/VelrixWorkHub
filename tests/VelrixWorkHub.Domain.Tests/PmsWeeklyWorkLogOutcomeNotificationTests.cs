using VelrixWorkHub.Application.Employees;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PmsWeeklyWorkLogOutcomeNotificationTests
{
    [Fact]
    public void WorkflowHandler_NotifiesEnabledProjectMemberWithApprovalResult()
    {
        var projectId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var item = new PmsWeeklyWorkLogSubmission(projectId, "项目经理", new DateOnly(2026, 7, 20), "[{\"hours\":8}]", 8m);
        item.Submit("project.manager", DateTime.Now);
        var notifications = new NotificationRepository();
        var handler = new PmsWeeklyWorkLogSubmissionWorkflowActionHandler(
            new SubmissionRepository(item),
            outcomeNotifications: new PmsWeeklyWorkLogOutcomeNotificationService(
                new MemberRepository(new PmsProjectMember(projectId, item.MemberName, "项目经理", userId: userId)),
                new EmployeeDirectoryService(new DirectoryRepository(new EmployeeDirectoryEntry(userId, "project.manager", "项目经理", null, null, true, null, null))),
                new NotificationService(notifications)));
        var instance = CreateInstance(item.Id);

        handler.Execute(new WorkflowActionContext(instance, WorkflowActionTrigger.Approved, null), new WorkflowActionDefinition(WorkflowActionType.SetField, nameof(PmsWeeklyWorkLogSubmission.Status), nameof(PmsWeeklyWorkLogSubmissionStatus.Approved)));

        var notification = Assert.Single(notifications.Items);
        Assert.Equal("project.manager", notification.Recipient);
        Assert.Equal("项目周工时已批准", notification.Title);
        Assert.Equal(PmsWeeklyWorkLogSubmissionStatus.Approved, item.Status);
    }

    [Fact]
    public void OutcomeNotification_SkipsUnboundDisabledOrAmbiguousMember()
    {
        var item = new PmsWeeklyWorkLogSubmission(Guid.CreateVersion7(), "项目经理", new DateOnly(2026, 7, 20), "[{\"hours\":8}]", 8m);
        var notifications = new NotificationRepository();
        var service = new PmsWeeklyWorkLogOutcomeNotificationService(new MemberRepository(), new EmployeeDirectoryService(new DirectoryRepository()), new NotificationService(notifications));

        service.Publish(item, Guid.CreateVersion7(), "Rejected", "请补充说明");

        Assert.Empty(notifications.Items);

        var firstUserId = Guid.CreateVersion7();
        var secondUserId = Guid.CreateVersion7();
        var ambiguousService = new PmsWeeklyWorkLogOutcomeNotificationService(
            new MemberRepository(
                new PmsProjectMember(item.ProjectId, item.MemberName, "角色一", userId: firstUserId),
                new PmsProjectMember(item.ProjectId, item.MemberName, "角色二", userId: secondUserId)),
            new EmployeeDirectoryService(new DirectoryRepository(
                new EmployeeDirectoryEntry(firstUserId, "first", item.MemberName, null, null, true, null, null),
                new EmployeeDirectoryEntry(secondUserId, "second", item.MemberName, null, null, true, null, null))),
            new NotificationService(notifications));

        ambiguousService.Publish(item, Guid.CreateVersion7(), "Rejected", "请补充说明");

        Assert.Empty(notifications.Items);
    }

    private static WorkflowInstance CreateInstance(Guid businessId)
    {
        var definition = new WorkflowDefinition(WorkflowBindingCodes.PmsWeeklyWorkLogApproval, "周工时审批");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, end.Id); definition.Publish();
        return WorkflowInstance.Start(definition, nameof(PmsWeeklyWorkLogSubmission), businessId, DateTime.Now);
    }

    private sealed class SubmissionRepository(params PmsWeeklyWorkLogSubmission[] items) : IPmsWeeklyWorkLogSubmissionRepository { private readonly List<PmsWeeklyWorkLogSubmission> data = [.. items]; public IReadOnlyList<PmsWeeklyWorkLogSubmission> List(Guid? projectId = null) => projectId is Guid id ? data.Where(x => x.ProjectId == id).ToArray() : data; public void Add(PmsWeeklyWorkLogSubmission item) => data.Add(item); public void Update(PmsWeeklyWorkLogSubmission item) { } public void Remove(Guid id) => data.RemoveAll(x => x.Id == id); }
    private sealed class MemberRepository(params PmsProjectMember[] items) : IPmsProjectMemberRepository { public IReadOnlyList<PmsProjectMember> List(Guid? projectId = null) => projectId is Guid id ? items.Where(x => x.ProjectId == id).ToArray() : items; public void Add(PmsProjectMember item) { } public void Update(PmsProjectMember item) { } public void Remove(Guid id) { } }
    private sealed class DirectoryRepository(params EmployeeDirectoryEntry[] items) : IEmployeeDirectoryRepository { public IReadOnlyList<EmployeeDirectoryEntry> List() => items; public IReadOnlyList<EmployeeDirectoryOrganization> ListOrganizations() => []; }
    private sealed class NotificationRepository : INotificationRepository { public List<WorkNotification> Items { get; } = []; public IReadOnlyList<WorkNotification> List(string recipient, bool unreadOnly = false) => Items.Where(x => x.Recipient.Equals(recipient, StringComparison.OrdinalIgnoreCase) && (!unreadOnly || !x.IsRead)).ToArray(); public WorkNotification? FindByDedupeKey(string recipient, string dedupeKey) => Items.FirstOrDefault(x => x.Recipient.Equals(recipient, StringComparison.OrdinalIgnoreCase) && x.DedupeKey == dedupeKey); public void Add(WorkNotification item) => Items.Add(item); public bool TryAdd(WorkNotification item) { if (FindByDedupeKey(item.Recipient, item.DedupeKey) is not null) return false; Add(item); return true; } public void Update(WorkNotification item) { } public int Delete(string recipient, IReadOnlyCollection<Guid> notificationIds) => 0; }
}
