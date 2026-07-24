using VelrixWorkHub.Application.PmpProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PmpResourceAllocationServiceTests
{
    [Fact]
    public void ResourceAllocation_CountsTasksAndHoursAndAppliesThresholds()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmpProject("PRJ-RESOURCE", "资源项目", null, null, today, today.AddDays(10));
        var member = new PmpProjectMember(project.Id, "成员 A", "开发", false, "研发部");
        var first = new PmpWbsTask(project.Id, null, "任务一", member.MemberName, 1, today, today.AddDays(2), false);
        var second = new PmpWbsTask(project.Id, null, "任务二", member.MemberName, 2, today, today.AddDays(2), false);
        var workLog = new PmpWorkLog(project.Id, first.Id, today, member.MemberName, 9, "集中开发");
        var service = new PmpResourceAllocationService(new ProjectRepository(project), new MemberRepository(member), new TaskRepository(first, second), new WorkLogRepository(workLog));

        var row = Assert.Single(service.List(today, today.AddDays(1), keyword: "研发部"));
        Assert.Equal("研发部", row.DepartmentName);
        Assert.Equal(2, row.Cells[0].TaskCount);
        Assert.Equal(9m, row.Cells[0].LoggedHours);
        Assert.Equal(PmpResourceLoadLevel.Overloaded, row.Cells[0].LoadLevel);
        Assert.Contains("任务一", row.Cells[0].TaskTitles);
        Assert.Throws<ArgumentException>(() => service.List(today, today.AddDays(33)));
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

    private sealed class TaskRepository(params PmpWbsTask[] items) : IPmpWbsTaskRepository
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
