using VelrixWorkHub.Application.PmpProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PmpExecutionGuardrailTests
{
    [Fact]
    public void PhaseService_EnforcesProjectRangeAndStatusFlow()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmpProject("PRJ-GUARD-PHASE", "阶段边界项目", null, null, today, today.AddDays(10));
        var phases = new PhaseRepository();
        var service = new PmpProjectPhaseService(phases, new ProjectRepository(project));

        Assert.Throws<InvalidOperationException>(() => service.Create(project.Id, "越界阶段", PmpProjectPhaseKind.Phase, 1, today.AddDays(-1), today));
        var phase = service.Create(project.Id, "阶段一", PmpProjectPhaseKind.Phase, 1, today, today.AddDays(3));
        service.SetStatus(phase, PmpProjectPhaseStatus.Active);
        service.SetStatus(phase, PmpProjectPhaseStatus.Completed);

        Assert.Equal(100, phase.PercentComplete);
        Assert.Throws<InvalidOperationException>(() => service.SetStatus(phase, PmpProjectPhaseStatus.Planned));
    }

    [Fact]
    public void WbsService_EnforcesParentDatesAndPreventsCycles()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmpProject("PRJ-GUARD-WBS", "WBS 边界项目", null, null, today, today.AddDays(10));
        var tasks = new WbsRepository();
        var service = new PmpWbsTaskService(tasks, new ProjectRepository(project));
        var parent = service.Create(project.Id, null, "父任务", null, 1, today, today.AddDays(5), false);

        Assert.Throws<InvalidOperationException>(() => service.Create(project.Id, parent.Id, "越界子任务", null, 1, today, today.AddDays(6), false));
        var child = service.Create(project.Id, parent.Id, "子任务", null, 1, today, today.AddDays(3), false);
        Assert.Throws<InvalidOperationException>(() => service.Edit(parent, child.Id, parent.Title, null, 1, today, today.AddDays(5), false));
        Assert.Throws<InvalidOperationException>(() => service.Edit(parent, null, parent.Title, null, 1, today, today.AddDays(2), false));

        service.SetStatus(child, PmpWbsTaskStatus.InProgress);
        service.SetStatus(child, PmpWbsTaskStatus.Done);
        Assert.Equal(100, child.PercentComplete);
    }

    [Fact]
    public void IssueService_EnforcesDueDateAndSupportsSequentialResolution()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmpProject("PRJ-GUARD-ISSUE", "风险边界项目", null, null, today, today.AddDays(10));
        var issues = new IssueRepository();
        var service = new PmpProjectIssueService(issues, new ProjectRepository(project));

        Assert.Throws<InvalidOperationException>(() => service.Create(project.Id, PmpProjectIssueKind.Risk, "越界风险", null, null, PmpProjectIssuePriority.High, today.AddDays(11)));
        var issue = service.Create(project.Id, PmpProjectIssueKind.Risk, "正常风险", null, null, PmpProjectIssuePriority.High, today.AddDays(5));
        service.SetStatus(issue, PmpProjectIssueStatus.InProgress);
        service.SetStatus(issue, PmpProjectIssueStatus.Resolved);
        service.SetStatus(issue, PmpProjectIssueStatus.Closed);

        Assert.Equal(PmpProjectIssueStatus.Closed, issue.Status);
    }

    [Fact]
    public void EvmService_CalculatesPlannedEarnedAndActualHoursFromExecutionData()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmpProject("PRJ-GUARD-EVM", "EVM 项目", null, null, today, today.AddDays(30));
        var first = new PmpWbsTask(project.Id, null, "任务一", null, 1, today, today.AddDays(2), false);
        first.SetPercentComplete(50);
        var second = new PmpWbsTask(project.Id, null, "任务二", null, 2, today, today.AddDays(1), false);
        second.SetPercentComplete(100);
        var workLog = new PmpWorkLog(project.Id, first.Id, today, "成员", 10, null);
        var service = new PmpEvmService(new ProjectRepository(project), new BaselineRepository(), new WbsRepository(first, second), new WorkLogRepository(workLog));

        var snapshot = Assert.Single(service.List());

        Assert.Equal(40m, snapshot.PlannedValue);
        Assert.Equal(28m, snapshot.EarnedValue);
        Assert.Equal(10m, snapshot.ActualCost);
        Assert.Equal(0.70m, snapshot.SchedulePerformanceIndex);
        Assert.Equal(2.80m, snapshot.CostPerformanceIndex);
    }

    private sealed class ProjectRepository(params PmpProject[] items) : IPmpProjectRepository
    {
        private readonly List<PmpProject> data = [.. items];
        public IReadOnlyList<PmpProject> List() => data;
        public void Add(PmpProject item) => data.Add(item);
        public void Update(PmpProject item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }

    private sealed class PhaseRepository(params PmpProjectPhase[] items) : IPmpProjectPhaseRepository
    {
        private readonly List<PmpProjectPhase> data = [.. items];
        public IReadOnlyList<PmpProjectPhase> List(Guid? projectId = null) => projectId is null ? data : data.Where(x => x.ProjectId == projectId).ToArray();
        public void Add(PmpProjectPhase item) => data.Add(item);
        public void Update(PmpProjectPhase item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }

    private sealed class WbsRepository(params PmpWbsTask[] items) : IPmpWbsTaskRepository
    {
        private readonly List<PmpWbsTask> data = [.. items];
        public IReadOnlyList<PmpWbsTask> List(Guid? projectId = null) => projectId is null ? data : data.Where(x => x.ProjectId == projectId).ToArray();
        public void Add(PmpWbsTask item) => data.Add(item);
        public void Update(PmpWbsTask item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }

    private sealed class IssueRepository : IPmpProjectIssueRepository
    {
        private readonly List<PmpProjectIssue> data = [];
        public IReadOnlyList<PmpProjectIssue> List(Guid? projectId = null) => projectId is null ? data : data.Where(x => x.ProjectId == projectId).ToArray();
        public void Add(PmpProjectIssue item) => data.Add(item);
        public void Update(PmpProjectIssue item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }

    private sealed class BaselineRepository : IPmpProjectBaselineRepository
    {
        public IReadOnlyList<PmpProjectBaseline> List(Guid? projectId = null) => [];
        public int NextVersion(Guid projectId) => 1;
        public void Add(PmpProjectBaseline item) { }
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
