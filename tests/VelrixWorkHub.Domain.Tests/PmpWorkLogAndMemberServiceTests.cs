using VelrixWorkHub.Application.PmpProjects;
using VelrixWorkHub.Application.Employees;
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
}
