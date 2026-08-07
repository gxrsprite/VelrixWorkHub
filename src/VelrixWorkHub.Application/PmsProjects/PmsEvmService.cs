using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.PmsProjects;

public sealed class PmsEvmService(IPmsProjectRepository projectRepository, IPmsProjectBaselineRepository baselineRepository, IPmsWbsTaskRepository taskRepository, IPmsWorkLogRepository workLogRepository)
{
    public IReadOnlyList<PmsEvmSnapshot> List() => projectRepository.List().Select(Build).ToArray();

    private PmsEvmSnapshot Build(PmsProject project)
    {
        var tasks = taskRepository.List(project.Id).Where(x => !x.IsMilestone).ToArray();
        var plannedHours = tasks.Sum(x => (x.PlannedEnd.DayNumber - x.PlannedStart.DayNumber + 1) * 8m);
        var earnedHours = tasks.Sum(x => (x.PlannedEnd.DayNumber - x.PlannedStart.DayNumber + 1) * 8m * x.PercentComplete / 100m);
        var actualHours = workLogRepository.List(project.Id).Sum(x => x.Hours);
        var baseline = baselineRepository.List(project.Id).FirstOrDefault();
        return new PmsEvmSnapshot(project, baseline, plannedHours, earnedHours, actualHours);
    }
}

public sealed record PmsEvmSnapshot(PmsProject Project, PmsProjectBaseline? Baseline, decimal PlannedValue, decimal EarnedValue, decimal ActualCost)
{
    public decimal SchedulePerformanceIndex => PlannedValue == 0 ? 0 : decimal.Round(EarnedValue / PlannedValue, 2);
    public decimal CostPerformanceIndex => ActualCost == 0 ? 0 : decimal.Round(EarnedValue / ActualCost, 2);
    public decimal VarianceAtCompletion => PlannedValue - ActualCost;
}
