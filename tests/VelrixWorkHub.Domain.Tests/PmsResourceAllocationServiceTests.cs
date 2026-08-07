using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PmsResourceAllocationServiceTests
{
    [Fact]
    public void ResourceAllocation_CountsTasksAndHoursAndAppliesThresholds()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmsProject("PRJ-RESOURCE", "资源项目", null, null, today, today.AddDays(10));
        var member = new PmsProjectMember(project.Id, "成员 A", "开发", false, "研发部");
        var first = new PmsWbsTask(project.Id, null, "任务一", member.MemberName, 1, today, today.AddDays(2), false);
        var second = new PmsWbsTask(project.Id, null, "任务二", member.MemberName, 2, today, today.AddDays(2), false);
        var workLog = new PmsWorkLog(project.Id, first.Id, today, member.MemberName, 9, "集中开发");
        var service = new PmsResourceAllocationService(new ProjectRepository(project), new MemberRepository(member), new TaskRepository(first, second), new WorkLogRepository(workLog));

        var row = Assert.Single(service.List(today, today.AddDays(1), keyword: "研发部"));
        Assert.Equal("研发部", row.DepartmentName);
        Assert.Equal(2, row.Cells[0].TaskCount);
        Assert.Equal(9m, row.Cells[0].LoggedHours);
        Assert.Equal(PmsResourceLoadLevel.Overloaded, row.Cells[0].LoadLevel);
        Assert.Contains("任务一", row.Cells[0].TaskTitles);
        Assert.Throws<ArgumentException>(() => service.List(today, today.AddDays(33)));
    }

    private sealed class ProjectRepository(params PmsProject[] items) : IPmsProjectRepository
    {
        private readonly List<PmsProject> data = [.. items];
        public IReadOnlyList<PmsProject> List() => data;
        public void Add(PmsProject item) => data.Add(item);
        public void Update(PmsProject item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }

    private sealed class MemberRepository(params PmsProjectMember[] items) : IPmsProjectMemberRepository
    {
        private readonly List<PmsProjectMember> data = [.. items];
        public IReadOnlyList<PmsProjectMember> List(Guid? projectId = null) => projectId is null ? data : data.Where(x => x.ProjectId == projectId).ToArray();
        public void Add(PmsProjectMember item) => data.Add(item);
        public void Update(PmsProjectMember item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }

    private sealed class TaskRepository(params PmsWbsTask[] items) : IPmsWbsTaskRepository
    {
        private readonly List<PmsWbsTask> data = [.. items];
        public IReadOnlyList<PmsWbsTask> List(Guid? projectId = null) => projectId is null ? data : data.Where(x => x.ProjectId == projectId).ToArray();
        public void Add(PmsWbsTask item) => data.Add(item);
        public void Update(PmsWbsTask item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }

    private sealed class WorkLogRepository(params PmsWorkLog[] items) : IPmsWorkLogRepository
    {
        private readonly List<PmsWorkLog> data = [.. items];
        public IReadOnlyList<PmsWorkLog> List(Guid? projectId = null) => projectId is null ? data : data.Where(x => x.ProjectId == projectId).ToArray();
        public void Add(PmsWorkLog item) => data.Add(item);
        public void Update(PmsWorkLog item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }
}
