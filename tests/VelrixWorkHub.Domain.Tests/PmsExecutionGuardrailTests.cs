using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PmsExecutionGuardrailTests
{
    [Fact]
    public void PhaseService_EnforcesProjectRangeAndStatusFlow()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmsProject("PRJ-GUARD-PHASE", "阶段边界项目", null, null, today, today.AddDays(10));
        var phases = new PhaseRepository();
        var service = new PmsProjectPhaseService(phases, new ProjectRepository(project));

        Assert.Throws<InvalidOperationException>(() => service.Create(project.Id, "越界阶段", PmsProjectPhaseKind.Phase, 1, today.AddDays(-1), today));
        var phase = service.Create(project.Id, "阶段一", PmsProjectPhaseKind.Phase, 1, today, today.AddDays(3));
        service.SetStatus(phase, PmsProjectPhaseStatus.Active);
        service.SetStatus(phase, PmsProjectPhaseStatus.Completed);

        Assert.Equal(100, phase.PercentComplete);
        Assert.Throws<InvalidOperationException>(() => service.SetStatus(phase, PmsProjectPhaseStatus.Planned));
    }

    [Fact]
    public void WbsService_EnforcesParentDatesAndPreventsCycles()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmsProject("PRJ-GUARD-WBS", "WBS 边界项目", null, null, today, today.AddDays(10));
        var tasks = new WbsRepository();
        var service = new PmsWbsTaskService(tasks, new ProjectRepository(project));
        var parent = service.Create(project.Id, null, "父任务", null, 1, today, today.AddDays(5), false);

        Assert.Throws<InvalidOperationException>(() => service.Create(project.Id, parent.Id, "越界子任务", null, 1, today, today.AddDays(6), false));
        var child = service.Create(project.Id, parent.Id, "子任务", null, 1, today, today.AddDays(3), false);
        Assert.Throws<InvalidOperationException>(() => service.Edit(parent, child.Id, parent.Title, null, 1, today, today.AddDays(5), false));
        Assert.Throws<InvalidOperationException>(() => service.Edit(parent, null, parent.Title, null, 1, today, today.AddDays(2), false));

        service.SetStatus(child, PmsWbsTaskStatus.InProgress);
        service.SetStatus(child, PmsWbsTaskStatus.Done);
        Assert.Equal(100, child.PercentComplete);
    }

    [Fact]
    public void IssueService_EnforcesDueDateAndSupportsSequentialResolution()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmsProject("PRJ-GUARD-ISSUE", "风险边界项目", null, null, today, today.AddDays(10));
        var issues = new IssueRepository();
        var service = new PmsProjectIssueService(issues, new ProjectRepository(project));

        Assert.Throws<InvalidOperationException>(() => service.Create(project.Id, PmsProjectIssueKind.Risk, "越界风险", null, null, PmsProjectIssuePriority.High, today.AddDays(11)));
        var issue = service.Create(project.Id, PmsProjectIssueKind.Risk, "正常风险", null, null, PmsProjectIssuePriority.High, today.AddDays(5));
        service.SetStatus(issue, PmsProjectIssueStatus.InProgress);
        service.SetStatus(issue, PmsProjectIssueStatus.Resolved);
        service.SetStatus(issue, PmsProjectIssueStatus.Closed);

        Assert.Equal(PmsProjectIssueStatus.Closed, issue.Status);
    }

    [Fact]
    public void EvmService_CalculatesPlannedEarnedAndActualHoursFromExecutionData()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmsProject("PRJ-GUARD-EVM", "EVM 项目", null, null, today, today.AddDays(30));
        var first = new PmsWbsTask(project.Id, null, "任务一", null, 1, today, today.AddDays(2), false);
        first.SetPercentComplete(50);
        var second = new PmsWbsTask(project.Id, null, "任务二", null, 2, today, today.AddDays(1), false);
        second.SetPercentComplete(100);
        var workLog = new PmsWorkLog(project.Id, first.Id, today, "成员", 10, null);
        var service = new PmsEvmService(new ProjectRepository(project), new BaselineRepository(), new WbsRepository(first, second), new WorkLogRepository(workLog));

        var snapshot = Assert.Single(service.List());

        Assert.Equal(40m, snapshot.PlannedValue);
        Assert.Equal(28m, snapshot.EarnedValue);
        Assert.Equal(10m, snapshot.ActualCost);
        Assert.Equal(0.70m, snapshot.SchedulePerformanceIndex);
        Assert.Equal(2.80m, snapshot.CostPerformanceIndex);
    }

    private sealed class ProjectRepository(params PmsProject[] items) : IPmsProjectRepository
    {
        private readonly List<PmsProject> data = [.. items];
        public IReadOnlyList<PmsProject> List() => data;
        public void Add(PmsProject item) => data.Add(item);
        public void Update(PmsProject item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }

    private sealed class PhaseRepository(params PmsProjectPhase[] items) : IPmsProjectPhaseRepository
    {
        private readonly List<PmsProjectPhase> data = [.. items];
        public IReadOnlyList<PmsProjectPhase> List(Guid? projectId = null) => projectId is null ? data : data.Where(x => x.ProjectId == projectId).ToArray();
        public void Add(PmsProjectPhase item) => data.Add(item);
        public void Update(PmsProjectPhase item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }

    private sealed class WbsRepository(params PmsWbsTask[] items) : IPmsWbsTaskRepository
    {
        private readonly List<PmsWbsTask> data = [.. items];
        public IReadOnlyList<PmsWbsTask> List(Guid? projectId = null) => projectId is null ? data : data.Where(x => x.ProjectId == projectId).ToArray();
        public void Add(PmsWbsTask item) => data.Add(item);
        public void Update(PmsWbsTask item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }

    private sealed class IssueRepository : IPmsProjectIssueRepository
    {
        private readonly List<PmsProjectIssue> data = [];
        public IReadOnlyList<PmsProjectIssue> List(Guid? projectId = null) => projectId is null ? data : data.Where(x => x.ProjectId == projectId).ToArray();
        public void Add(PmsProjectIssue item) => data.Add(item);
        public void Update(PmsProjectIssue item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }

    private sealed class BaselineRepository : IPmsProjectBaselineRepository
    {
        public IReadOnlyList<PmsProjectBaseline> List(Guid? projectId = null) => [];
        public int NextVersion(Guid projectId) => 1;
        public void Add(PmsProjectBaseline item) { }
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
