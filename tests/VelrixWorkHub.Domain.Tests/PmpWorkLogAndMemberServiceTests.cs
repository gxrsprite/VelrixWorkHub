using VelrixWorkHub.Application.PmpProjects;
using VelrixWorkHub.Application.Employees;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PmpWorkLogAndMemberServiceTests
{
    [Fact]
    public void WorkLogService_RequiresProjectMemberAndProjectDateRange()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmpProject("PRJ-WORKLOG", "工时一致性项目", null, null, today, today.AddDays(10));
        var member = new PmpProjectMember(project.Id, "项目经理", "项目主责人", true);
        var task = new PmpWbsTask(project.Id, null, "需求确认", "项目经理", 1, today, today.AddDays(5), false);
        var logs = new WorkLogRepository();
        var service = new PmpWorkLogService(logs, new ProjectRepository(project), new WbsRepository(task), new MemberRepository(member));

        Assert.Throws<InvalidOperationException>(() => service.Create(project.Id, task.Id, today, "外部成员", 2, null));
        Assert.Throws<InvalidOperationException>(() => service.Create(project.Id, task.Id, today.AddDays(11), "项目经理", 2, null));

        var item = service.Create(project.Id, task.Id, today.AddDays(1), " 项目经理 ", 2.345m, "需求确认");
        Assert.Equal("项目经理", item.MemberName);
        Assert.Equal(2.34m, item.Hours);
        Assert.Equal(2.34m, service.TotalHours(project.Id));
        Assert.Single(logs.List(project.Id));
    }

    [Fact]
    public void ProjectMemberService_EnforcesUniqueNamesAndSinglePrimary()
    {
        var project = new PmpProject("PRJ-MEMBER", "成员治理项目", null, null, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(10)));
        var primary = new PmpProjectMember(project.Id, "项目经理", "项目主责人", true);
        var second = new PmpProjectMember(project.Id, "业务负责人", "业务负责人");
        var members = new MemberRepository(primary, second);
        var service = new PmpProjectMemberService(members, new ProjectRepository(project));

        service.SetPrimary(second, true);

        Assert.False(primary.IsPrimary);
        Assert.True(second.IsPrimary);
        Assert.Throws<InvalidOperationException>(() => service.Create(project.Id, " 项目经理 ", "交付成员", false));
        Assert.Throws<ArgumentException>(() => service.Create(project.Id, "", "交付成员", false));
        Assert.Throws<ArgumentException>(() => service.Create(project.Id, "新成员", "", false));
    }

    [Fact]
    public void ProjectMemberService_ResolvesEnabledDirectoryPeopleToStableIdentityAndSnapshots()
    {
        var project = new PmpProject("PRJ-DIRECTORY", "目录成员项目", null, null, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(10)));
        var enabled = new EmployeeDirectoryEntry(Guid.CreateVersion7(), "delivery", "交付负责人", Guid.CreateVersion7(), "交付部", true, null, null);
        var disabled = new EmployeeDirectoryEntry(Guid.CreateVersion7(), "former", "已停用成员", null, null, false, null, null);
        var members = new MemberRepository();
        var service = new PmpProjectMemberService(members, new ProjectRepository(project), new EmployeeDirectoryService(new DirectoryRepository(enabled, disabled)));

        var item = service.CreateForPerson(project.Id, enabled.UserId, "交付负责人", true);

        Assert.Equal(enabled.UserId, item.UserId);
        Assert.Equal("交付负责人", item.MemberName);
        Assert.Equal("交付部", item.DepartmentName);
        service.Edit(item, "交付负责人（历史兼容编辑）", "交付负责人", "交付部");
        Assert.Equal(enabled.UserId, item.UserId);
        Assert.Throws<InvalidOperationException>(() => service.CreateForPerson(project.Id, enabled.UserId, "实施", false));
        Assert.Throws<ArgumentException>(() => service.CreateForPerson(project.Id, disabled.UserId, "实施", false));
        Assert.Throws<InvalidOperationException>(() => service.Create(project.Id, "交付负责人（历史兼容编辑）", "历史文本成员", false));
    }

    [Fact]
    public void ProjectMemberService_RejectsTwoDirectoryUsersWithTheSameDisplayName()
    {
        var project = new PmpProject("PRJ-DIRECTORY-NAME", "目录重名项目", null, null, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(10)));
        var first = new EmployeeDirectoryEntry(Guid.CreateVersion7(), "delivery-a", "同名成员", Guid.CreateVersion7(), "交付部", true, null, null);
        var second = new EmployeeDirectoryEntry(Guid.CreateVersion7(), "delivery-b", "同名成员", Guid.CreateVersion7(), "实施部", true, null, null);
        var service = new PmpProjectMemberService(new MemberRepository(), new ProjectRepository(project), new EmployeeDirectoryService(new DirectoryRepository(first, second)));

        service.CreateForPerson(project.Id, first.UserId, "交付负责人", true);

        Assert.Throws<InvalidOperationException>(() => service.CreateForPerson(project.Id, second.UserId, "实施顾问", false));
    }

    [Fact]
    public void ProjectMemberService_ListProjectsForMemberReturnsOnlyStableUserMembershipProjects()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var mine = new PmpProject("PRJ-VISIBLE-MINE", "我的项目", null, null, today, today.AddDays(10));
        var other = new PmpProject("PRJ-VISIBLE-OTHER", "其他项目", null, null, today, today.AddDays(10));
        var userId = Guid.CreateVersion7();
        var service = new PmpProjectMemberService(
            new MemberRepository(
                new PmpProjectMember(mine.Id, "项目成员", "实施", userId: userId),
                new PmpProjectMember(other.Id, "其他成员", "实施", userId: Guid.CreateVersion7()),
                new PmpProjectMember(other.Id, "项目成员", "历史同名", userId: Guid.CreateVersion7())),
            new ProjectRepository(other, mine));

        var visible = service.ListProjectsForMember(userId);

        Assert.Equal([mine.Id], visible.Select(x => x.Id));
        Assert.Empty(service.ListProjectsForMember(Guid.CreateVersion7()));
        Assert.Empty(service.ListProjectsForMember(Guid.Empty));
    }

    [Fact]
    public void ProjectMemberService_ListProjectsForMemberExcludesAmbiguousLegacyMembership()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmpProject("PRJ-AMBIGUOUS-VISIBILITY", "歧义可见性项目", null, null, today, today.AddDays(10));
        var userId = Guid.CreateVersion7();
        var service = new PmpProjectMemberService(
            new MemberRepository(
                new PmpProjectMember(project.Id, "历史成员甲", "实施", userId: userId),
                new PmpProjectMember(project.Id, "历史成员乙", "实施", userId: userId)),
            new ProjectRepository(project));

        Assert.Empty(service.ListProjectsForMember(userId));
        Assert.Null(service.FindUniqueProjectMember(project.Id, userId));
    }

    [Fact]
    public void WorkLogMatrix_UpsertsAttendanceAndClearsZeroHours()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmpProject("PRJ-MATRIX", "矩阵项目", null, null, today, today.AddDays(10));
        var member = new PmpProjectMember(project.Id, "项目经理", "项目主责人");
        var task = new PmpWbsTask(project.Id, null, "需求确认", "项目经理", 1, today, today.AddDays(5), false);
        var logs = new WorkLogRepository();
        var service = new PmpWorkLogService(logs, new ProjectRepository(project), new WbsRepository(task), new MemberRepository(member));

        service.SaveCell(project.Id, task.Id, today, member.MemberName, 4.5m, PmpWorkLogAttendanceStatus.NoAttendance, "补录");
        service.SaveCell(project.Id, task.Id, today, member.MemberName, 6m, PmpWorkLogAttendanceStatus.Normal, "修正");
        Assert.Equal(6m, Assert.Single(logs.List(project.Id)).Hours);
        Assert.Equal(PmpWorkLogAttendanceStatus.Normal, logs.List(project.Id).Single().AttendanceStatus);

        service.SaveCell(project.Id, task.Id, today, member.MemberName, 0, PmpWorkLogAttendanceStatus.Normal, null);
        Assert.Empty(logs.List(project.Id));
    }

    [Fact]
    public void WorkLogService_RejectsMemberDailyHoursAboveTwentyFourAcrossTasks()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmpProject("PRJ-DAILY-HOURS", "日工时门禁项目", null, null, today, today.AddDays(10));
        var member = new PmpProjectMember(project.Id, "项目经理", "项目主责人");
        var firstTask = new PmpWbsTask(project.Id, null, "需求确认", "项目经理", 1, today, today.AddDays(5), false);
        var secondTask = new PmpWbsTask(project.Id, null, "方案评审", "项目经理", 2, today, today.AddDays(5), false);
        var logs = new WorkLogRepository();
        var service = new PmpWorkLogService(logs, new ProjectRepository(project), new WbsRepository(firstTask, secondTask), new MemberRepository(member));

        service.SaveCell(project.Id, firstTask.Id, today, member.MemberName, 14m, PmpWorkLogAttendanceStatus.Normal, null);
        service.SaveCell(project.Id, secondTask.Id, today, member.MemberName, 10m, PmpWorkLogAttendanceStatus.Normal, null);

        Assert.Throws<InvalidOperationException>(() => service.SaveCell(project.Id, secondTask.Id, today, member.MemberName, 10.1m, PmpWorkLogAttendanceStatus.Normal, null));
        Assert.Equal(24m, service.TotalHours(project.Id));
    }

    [Fact]
    public void WorkLogService_ValidatesWholeBatchBeforeChangingAnyCell()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmpProject("PRJ-BATCH-HOURS", "批量工时门禁项目", null, null, today, today.AddDays(10));
        var member = new PmpProjectMember(project.Id, "项目经理", "项目主责人");
        var firstTask = new PmpWbsTask(project.Id, null, "需求确认", "项目经理", 1, today, today.AddDays(5), false);
        var secondTask = new PmpWbsTask(project.Id, null, "方案评审", "项目经理", 2, today, today.AddDays(5), false);
        var logs = new WorkLogRepository();
        var service = new PmpWorkLogService(logs, new ProjectRepository(project), new WbsRepository(firstTask, secondTask), new MemberRepository(member));
        service.SaveCell(project.Id, firstTask.Id, today, member.MemberName, 10m, PmpWorkLogAttendanceStatus.Normal, "原始工时");

        Assert.Throws<InvalidOperationException>(() => service.SaveCells(project.Id,
        [
            new PmpWorkLogCellSave(firstTask.Id, today, member.MemberName, 14m, PmpWorkLogAttendanceStatus.Normal, "待保存修改"),
            new PmpWorkLogCellSave(secondTask.Id, today, member.MemberName, 11m, PmpWorkLogAttendanceStatus.Normal, "超出累计")
        ]));

        var saved = Assert.Single(logs.List(project.Id));
        Assert.Equal(10m, saved.Hours);
        Assert.Equal("原始工时", saved.Note);
    }

    [Fact]
    public void WorkLogService_SaveCellsForMemberRejectsAnotherMembersCells()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmpProject("PRJ-OWN-WORKLOG", "本人填报门禁项目", null, null, today, today.AddDays(10));
        var owner = new PmpProjectMember(project.Id, "项目经理", "项目主责人");
        var other = new PmpProjectMember(project.Id, "实施顾问", "交付成员");
        var task = new PmpWbsTask(project.Id, null, "需求确认", "项目经理", 1, today, today.AddDays(5), false);
        var service = new PmpWorkLogService(new WorkLogRepository(), new ProjectRepository(project), new WbsRepository(task), new MemberRepository(owner, other));

        Assert.Throws<UnauthorizedAccessException>(() => service.SaveCellsForMember(project.Id, owner.MemberName,
            [new PmpWorkLogCellSave(task.Id, today, other.MemberName, 8m, PmpWorkLogAttendanceStatus.Normal, null)]));
    }

    [Fact]
    public void WorkLogService_SaveCellsForProjectMemberRequiresStableProjectUserId()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmpProject("PRJ-STABLE-WORKLOG", "稳定身份工时项目", null, null, today, today.AddDays(10));
        var userId = Guid.CreateVersion7();
        var member = new PmpProjectMember(project.Id, "项目经理", "项目主责人", userId: userId);
        var task = new PmpWbsTask(project.Id, null, "需求确认", "项目经理", 1, today, today.AddDays(5), false);
        var logs = new WorkLogRepository();
        var service = new PmpWorkLogService(logs, new ProjectRepository(project), new WbsRepository(task), new MemberRepository(member));

        Assert.Throws<UnauthorizedAccessException>(() => service.SaveCellsForProjectMember(project.Id, Guid.CreateVersion7(), [new PmpWorkLogCellSave(task.Id, today, member.MemberName, 8m, PmpWorkLogAttendanceStatus.Normal, null)]));
        service.SaveCellsForProjectMember(project.Id, userId, [new PmpWorkLogCellSave(task.Id, today, member.MemberName, 8m, PmpWorkLogAttendanceStatus.Normal, null)]);
        Assert.Equal(8m, Assert.Single(logs.List(project.Id)).Hours);
    }

    [Fact]
    public void WorkLogService_ListForProjectMemberUsesStableUserIdAndDoesNotReturnOtherMembersLogs()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmpProject("PRJ-READ-SCOPE", "工时读取范围项目", null, null, today, today.AddDays(10));
        var userId = Guid.CreateVersion7();
        var mine = new PmpProjectMember(project.Id, "我的成员", "实施", userId: userId);
        var other = new PmpProjectMember(project.Id, "其他成员", "实施", userId: Guid.CreateVersion7());
        var logs = new WorkLogRepository(
            new PmpWorkLog(project.Id, null, today, mine.MemberName, 8m, null),
            new PmpWorkLog(project.Id, null, today, other.MemberName, 6m, null));
        var service = new PmpWorkLogService(logs, new ProjectRepository(project), new WbsRepository(), new MemberRepository(mine, other));

        var visible = service.ListForProjectMember(project.Id, userId);

        var item = Assert.Single(visible);
        Assert.Equal(mine.MemberName, item.MemberName);
        Assert.Empty(service.ListForProjectMember(project.Id, Guid.CreateVersion7()));
        Assert.Empty(service.ListForProjectMember(project.Id, Guid.Empty));
    }

    [Fact]
    public void WorkLogService_RejectsAmbiguousLegacyMembershipForReadAndWrite()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmpProject("PRJ-AMBIGUOUS-MEMBER", "歧义成员项目", null, null, today, today.AddDays(10));
        var userId = Guid.CreateVersion7();
        var first = new PmpProjectMember(project.Id, "历史成员甲", "实施", userId: userId);
        var second = new PmpProjectMember(project.Id, "历史成员乙", "实施", userId: userId);
        var service = new PmpWorkLogService(
            new WorkLogRepository(new PmpWorkLog(project.Id, null, today, first.MemberName, 8m, null)),
            new ProjectRepository(project), new WbsRepository(), new MemberRepository(first, second));

        Assert.Empty(service.ListForProjectMember(project.Id, userId));
        Assert.Throws<UnauthorizedAccessException>(() => service.SaveCellsForProjectMember(project.Id, userId,
            [new PmpWorkLogCellSave(null, today, first.MemberName, 8m, PmpWorkLogAttendanceStatus.Normal, null)]));
    }

    [Fact]
    public void WorkLogService_SavesWholeBatchInsideConfiguredTransactionBoundary()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmpProject("PRJ-BATCH-TX", "批量工时事务项目", null, null, today, today.AddDays(10));
        var member = new PmpProjectMember(project.Id, "项目经理", "项目主责人");
        var task = new PmpWbsTask(project.Id, null, "需求确认", "项目经理", 1, today, today.AddDays(5), false);
        var boundary = new RecordingTransactionBoundary();
        var service = new PmpWorkLogService(new WorkLogRepository(), new ProjectRepository(project), new WbsRepository(task), new MemberRepository(member), boundary);

        service.SaveCells(project.Id, [new PmpWorkLogCellSave(task.Id, today, member.MemberName, 8m, PmpWorkLogAttendanceStatus.Normal, null)]);

        Assert.Equal(1, boundary.ExecuteCount);
    }

    private sealed class ProjectRepository(params PmpProject[] items) : IPmpProjectRepository
    {
        private readonly List<PmpProject> data = [.. items];
        public IReadOnlyList<PmpProject> List() => data;
        public void Add(PmpProject item) => data.Add(item);
        public void Update(PmpProject item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }

    private sealed class MemberRepository(params PmpProjectMember[] items) : IPmpProjectMemberRepository
    {
        private readonly List<PmpProjectMember> data = [.. items];
        public IReadOnlyList<PmpProjectMember> List(Guid? projectId = null) => projectId is null ? data : data.Where(x => x.ProjectId == projectId).ToArray();
        public void Add(PmpProjectMember item) => data.Add(item);
        public void Update(PmpProjectMember item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }

    private sealed class DirectoryRepository(params EmployeeDirectoryEntry[] items) : IEmployeeDirectoryRepository
    {
        public IReadOnlyList<EmployeeDirectoryEntry> List() => items;
        public IReadOnlyList<EmployeeDirectoryOrganization> ListOrganizations() => [];
    }

    private sealed class WbsRepository(params PmpWbsTask[] items) : IPmpWbsTaskRepository
    {
        private readonly List<PmpWbsTask> data = [.. items];
        public IReadOnlyList<PmpWbsTask> List(Guid? projectId = null) => projectId is null ? data : data.Where(x => x.ProjectId == projectId).ToArray();
        public void Add(PmpWbsTask item) => data.Add(item);
        public void Update(PmpWbsTask item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }

    private sealed class WorkLogRepository(params PmpWorkLog[] items) : IPmpWorkLogRepository
    {
        private readonly List<PmpWorkLog> data = [.. items];
        public IReadOnlyList<PmpWorkLog> List(Guid? projectId = null) => projectId is null ? data : data.Where(x => x.ProjectId == projectId).ToArray();
        public void Add(PmpWorkLog item) => data.Add(item);
        public void Update(PmpWorkLog item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }

    private sealed class RecordingTransactionBoundary : IWorkflowTransactionBoundary
    {
        public int ExecuteCount { get; private set; }
        public void Execute(Action operation, Action<Exception>? afterRollback = null) { ExecuteCount++; operation(); }
    }
}
