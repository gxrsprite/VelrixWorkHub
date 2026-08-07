using FreeSql;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmsProjects;

public static class PmsProjectPhaseSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<PmsProjectPhaseRecord>();
        if (fsql.Select<PmsProjectPhaseRecord>().Any()) return;
        var project = fsql.Select<PmsProjectRecord>().OrderBy(x => x.CreatedTime).First();
        if (project is null) return;
        var start = DateOnly.FromDateTime(project.PlannedStart);
        var phase = new PmsProjectPhase(project.Id, "需求与方案确认", PmsProjectPhaseKind.Phase, 1, start, start.AddDays(14));
        var milestone = new PmsProjectPhase(project.Id, "方案评审完成", PmsProjectPhaseKind.Milestone, 2, start.AddDays(14), start.AddDays(14));
        var now = DateTime.Now;
        fsql.Insert(new[] { phase, milestone }.Select(x => new PmsProjectPhaseRecord { Id = x.Id, ProjectId = x.ProjectId, Name = x.Name, Kind = x.Kind, Sequence = x.Sequence, PlannedStart = x.PlannedStart.ToDateTime(TimeOnly.MinValue), PlannedEnd = x.PlannedEnd.ToDateTime(TimeOnly.MinValue), PercentComplete = x.PercentComplete, Status = x.Status, CreatedTime = now, ModifiedTime = now })).ExecuteAffrows();
    }
}
