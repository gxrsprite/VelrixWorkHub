using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.PmpProjects;

public sealed class PmpEvmService(IPmpProjectRepository projectRepository, IPmpProjectBaselineRepository baselineRepository, IPmpWbsTaskRepository taskRepository, IPmpWorkLogRepository workLogRepository)
{
    public IReadOnlyList<PmpEvmSnapshot> List() => projectRepository.List().Select(Build).ToArray();

    private PmpEvmSnapshot Build(PmpProject project)
    {
        var tasks = taskRepository.List(project.Id).Where(x => !x.IsMilestone).ToArray();
        var plannedHours = tasks.Sum(x => (x.PlannedEnd.DayNumber - x.PlannedStart.DayNumber + 1) * 8m);
        var earnedHours = tasks.Sum(x => (x.PlannedEnd.DayNumber - x.PlannedStart.DayNumber + 1) * 8m * x.PercentComplete / 100m);
        var actualHours = workLogRepository.List(project.Id).Sum(x => x.Hours);
        var baseline = baselineRepository.List(project.Id).FirstOrDefault();
        return new PmpEvmSnapshot(project, baseline, plannedHours, earnedHours, actualHours);
    }
}

public sealed record PmpEvmSnapshot(PmpProject Project, PmpProjectBaseline? Baseline, decimal PlannedValue, decimal EarnedValue, decimal ActualCost)
{
    public decimal SchedulePerformanceIndex => PlannedValue == 0 ? 0 : decimal.Round(EarnedValue / PlannedValue, 2);
    public decimal CostPerformanceIndex => ActualCost == 0 ? 0 : decimal.Round(EarnedValue / ActualCost, 2);
    public decimal VarianceAtCompletion => PlannedValue - ActualCost;
}
