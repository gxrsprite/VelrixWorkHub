using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmsProjects;

public sealed class PmsProjectBaselineService(IPmsProjectBaselineRepository repository, IPmsProjectRepository projectRepository, IPmsProjectPhaseRepository phaseRepository, IPmsWbsTaskRepository taskRepository)
{
    public IReadOnlyList<PmsProjectBaseline> List(Guid? projectId = null) => repository.List(projectId).OrderByDescending(x => x.SnapshotTime).ToArray();

    public PmsBaselineComparison? Compare(PmsProjectBaseline baseline)
    {
        var project = projectRepository.List().FirstOrDefault(x => x.Id == baseline.ProjectId);
        if (project is null) return null;
        var phaseCount = phaseRepository.List(project.Id).Count;
        var taskCount = taskRepository.List(project.Id).Count;
        return new PmsBaselineComparison(baseline, project, phaseCount, taskCount);
    }

    public PmsProjectBaseline Create(Guid projectId, string label)
    {
        var project = projectRepository.List().FirstOrDefault(x => x.Id == projectId) ?? throw new InvalidOperationException("关联项目不存在。");
        var item = new PmsProjectBaseline(project.Id, repository.NextVersion(project.Id), label, DateTime.Now, project.PlannedStart, project.PlannedEnd, project.PercentComplete, phaseRepository.List(project.Id).Count, taskRepository.List(project.Id).Count);
        repository.Add(item);
        return item;
    }
}

public sealed record PmsBaselineComparison(PmsProjectBaseline Baseline, PmsProject CurrentProject, int CurrentPhaseCount, int CurrentTaskCount)
{
    public int PercentDelta => CurrentProject.PercentComplete - Baseline.PercentComplete;
    public int PhaseDelta => CurrentPhaseCount - Baseline.PhaseCount;
    public int TaskDelta => CurrentTaskCount - Baseline.TaskCount;
    public int PlannedDaysDelta => CurrentProject.PlannedEnd.DayNumber - CurrentProject.PlannedStart.DayNumber - (Baseline.PlannedEnd.DayNumber - Baseline.PlannedStart.DayNumber);
}
