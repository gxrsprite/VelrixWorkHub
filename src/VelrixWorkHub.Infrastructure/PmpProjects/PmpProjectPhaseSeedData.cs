using FreeSql;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmpProjects;

public static class PmpProjectPhaseSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<PmpProjectPhaseRecord>();
        if (fsql.Select<PmpProjectPhaseRecord>().Any()) return;
        var project = fsql.Select<PmpProjectRecord>().OrderBy(x => x.CreatedTime).First();
        if (project is null) return;
        var start = DateOnly.FromDateTime(project.PlannedStart);
        var phase = new PmpProjectPhase(project.Id, "需求与方案确认", PmpProjectPhaseKind.Phase, 1, start, start.AddDays(14));
        var milestone = new PmpProjectPhase(project.Id, "方案评审完成", PmpProjectPhaseKind.Milestone, 2, start.AddDays(14), start.AddDays(14));
        var now = DateTime.Now;
        fsql.Insert(new[] { phase, milestone }.Select(x => new PmpProjectPhaseRecord { Id = x.Id, ProjectId = x.ProjectId, Name = x.Name, Kind = x.Kind, Sequence = x.Sequence, PlannedStart = x.PlannedStart.ToDateTime(TimeOnly.MinValue), PlannedEnd = x.PlannedEnd.ToDateTime(TimeOnly.MinValue), PercentComplete = x.PercentComplete, Status = x.Status, CreatedTime = now, ModifiedTime = now })).ExecuteAffrows();
    }
}
