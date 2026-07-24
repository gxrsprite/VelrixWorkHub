using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmpProjects;

public sealed class PmpProjectBaselineService(IPmpProjectBaselineRepository repository, IPmpProjectRepository projectRepository, IPmpProjectPhaseRepository phaseRepository, IPmpWbsTaskRepository taskRepository)
{
    public IReadOnlyList<PmpProjectBaseline> List(Guid? projectId = null) => repository.List(projectId).OrderByDescending(x => x.SnapshotTime).ToArray();

    public PmpBaselineComparison? Compare(PmpProjectBaseline baseline)
    {
        var project = projectRepository.List().FirstOrDefault(x => x.Id == baseline.ProjectId);
        if (project is null) return null;
        var phaseCount = phaseRepository.List(project.Id).Count;
        var taskCount = taskRepository.List(project.Id).Count;
        return new PmpBaselineComparison(baseline, project, phaseCount, taskCount);
    }

    public PmpProjectBaseline Create(Guid projectId, string label)
    {
        var project = projectRepository.List().FirstOrDefault(x => x.Id == projectId) ?? throw new InvalidOperationException("关联项目不存在。");
        var item = new PmpProjectBaseline(project.Id, repository.NextVersion(project.Id), label, DateTime.Now, project.PlannedStart, project.PlannedEnd, project.PercentComplete, phaseRepository.List(project.Id).Count, taskRepository.List(project.Id).Count);
        repository.Add(item);
        return item;
    }
}

public sealed record PmpBaselineComparison(PmpProjectBaseline Baseline, PmpProject CurrentProject, int CurrentPhaseCount, int CurrentTaskCount)
{
    public int PercentDelta => CurrentProject.PercentComplete - Baseline.PercentComplete;
    public int PhaseDelta => CurrentPhaseCount - Baseline.PhaseCount;
    public int TaskDelta => CurrentTaskCount - Baseline.TaskCount;
    public int PlannedDaysDelta => CurrentProject.PlannedEnd.DayNumber - CurrentProject.PlannedStart.DayNumber - (Baseline.PlannedEnd.DayNumber - Baseline.PlannedStart.DayNumber);
}
