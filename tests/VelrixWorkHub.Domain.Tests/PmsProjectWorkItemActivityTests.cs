using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Application.Employees;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PmsProjectWorkItemActivityTests
{
    [Fact]
    public void WorkItemService_WritesImmutableCommentsAndStatusActivities()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmsProject("PRJ-ACTIVITY", "活动项目", null, null, today, today.AddDays(30));
        var workItems = new WorkItemRepository();
        var activities = new ActivityRepository();
        var service = new PmsProjectWorkItemService(workItems, new ProjectRepository(project), activities);
        var item = service.Create(project.Id, null, null, null, "整理验收材料", null, "项目经理", null, PmsProjectWorkItemPriority.High, null, null, "{}");

        service.SetStatus(item, PmsProjectWorkItemStatus.Open, null, "项目经理");
        service.AddComment(item, "请在周五前提交初稿。", "项目经理");

        var history = service.ListActivities(item.Id);
        Assert.Equal(3, history.Count);
        Assert.Equal(PmsProjectWorkItemActivityType.Commented, history[0].Type);
        Assert.Equal("请在周五前提交初稿。", history[0].Content);
        Assert.Contains(history, x => x.Type == PmsProjectWorkItemActivityType.StatusChanged && x.PreviousStatus == PmsProjectWorkItemStatus.Draft && x.CurrentStatus == PmsProjectWorkItemStatus.Open);
        Assert.Throws<InvalidOperationException>(() => service.AddComment(new PmsProjectWorkItem(project.Id, null, null, null, "伪造工作项", null, null, null, PmsProjectWorkItemPriority.Low, null, null, "{}"), "不应写入", "项目经理"));
    }

    [Fact]
    public void WorkItemService_ResolvesEnabledPeopleToDisplayNameSnapshots()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmsProject("PRJ-PEOPLE", "人员项目", null, null, today, today.AddDays(30));
        var owner = new EmployeeDirectoryEntry(Guid.CreateVersion7(), "owner", "负责人甲", null, null, true, null, null);
        var participant = new EmployeeDirectoryEntry(Guid.CreateVersion7(), "member", "参与人乙", null, null, true, null, null);
        var disabled = new EmployeeDirectoryEntry(Guid.CreateVersion7(), "disabled", "停用人员", null, null, false, null, null);
        var service = new PmsProjectWorkItemService(new WorkItemRepository(), new ProjectRepository(project), null, new EmployeeDirectoryService(new DirectoryRepository(owner, participant, disabled)));

        var item = service.CreateForPeople(project.Id, null, null, null, "受控人员工作项", null, owner.UserId, [participant.UserId], PmsProjectWorkItemPriority.Medium, null, null, "{}");

        Assert.Equal("负责人甲", item.OwnerName);
        Assert.Equal(owner.UserId, item.OwnerUserId);
        Assert.Equal("参与人乙", item.ParticipantNames);
        Assert.Equal([participant.UserId], item.ParticipantUserIds);
        Assert.Single(service.ListVisible(owner.UserId, isAdministrator: false));
        Assert.Single(service.ListVisible(participant.UserId, isAdministrator: false));
        Assert.Empty(service.ListVisible(Guid.CreateVersion7(), isAdministrator: false));
        Assert.Throws<ArgumentException>(() => service.CreateForPeople(project.Id, null, null, null, "停用负责人", null, disabled.UserId, [], PmsProjectWorkItemPriority.Low, null, null, "{}"));
        Assert.Throws<ArgumentException>(() => service.CreateForPeople(project.Id, null, null, null, "重复参与人", null, null, [participant.UserId, participant.UserId], PmsProjectWorkItemPriority.Low, null, null, "{}"));
        Assert.Throws<ArgumentException>(() => service.CreateForPeople(project.Id, null, null, null, "负责人重复", null, owner.UserId, [owner.UserId], PmsProjectWorkItemPriority.Low, null, null, "{}"));
    }

    [Fact]
    public void WorkItemService_ProjectMembersCanReadProjectItemsThroughStableIdentity()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmsProject("PRJ-MEMBER-SCOPE", "成员范围项目", null, null, today, today.AddDays(30));
        var userId = Guid.CreateVersion7();
        var member = new PmsProjectMember(project.Id, "同名成员", "交付", false, null, userId);
        var service = new PmsProjectWorkItemService(new WorkItemRepository(), new ProjectRepository(project), members: new MemberRepository(member));

        service.Create(project.Id, null, null, null, "项目工作项", null, null, null, PmsProjectWorkItemPriority.Medium, null, null, "{}");

        Assert.Single(service.ListVisible(userId, isAdministrator: false));
        Assert.Empty(service.ListVisible(Guid.CreateVersion7(), isAdministrator: false));
    }

    [Fact]
    public void WorkItemService_GrantsVisibilityToSelectedDirectoryOrganization()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmsProject("PRJ-ORG-SCOPE", "部门范围项目", null, null, today, today.AddDays(30));
        var engineeringId = Guid.CreateVersion7();
        var salesId = Guid.CreateVersion7();
        var engineeringUser = new EmployeeDirectoryEntry(Guid.CreateVersion7(), "engineering", "研发人员", engineeringId, "研发部", true, null, null);
        var salesUser = new EmployeeDirectoryEntry(Guid.CreateVersion7(), "sales", "销售人员", salesId, "销售部", true, null, null);
        var service = new PmsProjectWorkItemService(new WorkItemRepository(), new ProjectRepository(project), directory: new EmployeeDirectoryService(new DirectoryRepository(engineeringUser, salesUser)));

        var scoped = service.Create(project.Id, null, null, null, "研发可见工作项", null, null, null, PmsProjectWorkItemPriority.Medium, null, null, "{}", visibilityOrganizationIds: [engineeringId, engineeringId, Guid.Empty]);
        service.Create(project.Id, null, null, null, "无部门授权工作项", null, null, null, PmsProjectWorkItemPriority.Medium, null, null, "{}");

        Assert.Equal([engineeringId], scoped.VisibilityOrganizationIds);
        Assert.Single(service.ListVisible(engineeringUser.UserId, isAdministrator: false));
        Assert.Empty(service.ListVisible(salesUser.UserId, isAdministrator: false));
        Assert.Throws<ArgumentException>(() => service.Create(project.Id, null, null, null, "无效部门工作项", null, null, null, PmsProjectWorkItemPriority.Low, null, null, "{}", visibilityOrganizationIds: [Guid.CreateVersion7()]));
    }

    [Fact]
    public void WorkItemService_GrantsVisibilityToSelectedPlatformRole()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmsProject("PRJ-ROLE-SCOPE", "角色范围项目", null, null, today, today.AddDays(30));
        var deliveryRole = new EmployeeDirectoryRole(Guid.CreateVersion7(), "交付经理");
        var deliveryUser = new EmployeeDirectoryEntry(Guid.CreateVersion7(), "delivery", "交付人员", null, null, true, null, null, [deliveryRole]);
        var unrelatedUser = new EmployeeDirectoryEntry(Guid.CreateVersion7(), "unrelated", "无关人员", null, null, true, null, null);
        var service = new PmsProjectWorkItemService(new WorkItemRepository(), new ProjectRepository(project), directory: new EmployeeDirectoryService(new DirectoryRepository(deliveryUser, unrelatedUser)));

        var scoped = service.Create(project.Id, null, null, null, "角色可见工作项", null, null, null, PmsProjectWorkItemPriority.Medium, null, null, "{}", visibilityRoleIds: [deliveryRole.Id]);

        Assert.Equal([deliveryRole.Id], scoped.VisibilityRoleIds);
        Assert.Single(service.ListVisible(deliveryUser.UserId, isAdministrator: false));
        Assert.Empty(service.ListVisible(unrelatedUser.UserId, isAdministrator: false));
        Assert.Throws<ArgumentException>(() => service.Create(project.Id, null, null, null, "无效角色工作项", null, null, null, PmsProjectWorkItemPriority.Low, null, null, "{}", visibilityRoleIds: [Guid.CreateVersion7()]));
    }

    private sealed class ProjectRepository(params PmsProject[] data) : IPmsProjectRepository { public IReadOnlyList<PmsProject> List() => data; public void Add(PmsProject item) { } public void Update(PmsProject item) { } public void Remove(Guid id) { } }
    private sealed class WorkItemRepository : IPmsProjectWorkItemRepository { private readonly List<PmsProjectWorkItem> data = []; public IReadOnlyList<PmsProjectWorkItem> List(Guid? projectId = null) => projectId is Guid id ? data.Where(x => x.ProjectId == id).ToArray() : data; public void Add(PmsProjectWorkItem item) => data.Add(item); public void Update(PmsProjectWorkItem item) { } public void Remove(Guid id) => data.RemoveAll(x => x.Id == id); }
    private sealed class ActivityRepository : IPmsProjectWorkItemActivityRepository { private readonly List<PmsProjectWorkItemActivity> data = []; public IReadOnlyList<PmsProjectWorkItemActivity> List(Guid workItemId) => data.Where(x => x.WorkItemId == workItemId).OrderByDescending(x => x.OccurredAt).ToArray(); public void Add(PmsProjectWorkItemActivity activity) => data.Add(activity); }
    private sealed class MemberRepository(params PmsProjectMember[] data) : IPmsProjectMemberRepository { public IReadOnlyList<PmsProjectMember> List(Guid? projectId = null) => projectId is Guid id ? data.Where(x => x.ProjectId == id).ToArray() : data; public void Add(PmsProjectMember item) { } public void Update(PmsProjectMember item) { } public void Remove(Guid id) { } }
    private sealed class DirectoryRepository(params EmployeeDirectoryEntry[] data) : IEmployeeDirectoryRepository { public IReadOnlyList<EmployeeDirectoryEntry> List() => data; public IReadOnlyList<EmployeeDirectoryOrganization> ListOrganizations() => data.Where(x => x.OrganizationId is Guid).GroupBy(x => x.OrganizationId!.Value).Select(x => new EmployeeDirectoryOrganization(x.Key, x.First().OrganizationName ?? "未命名组织")).ToArray(); public IReadOnlyList<EmployeeDirectoryRole> ListRoles() => data.SelectMany(x => x.Roles ?? []).GroupBy(x => x.Id).Select(x => x.First()).ToArray(); }
}
