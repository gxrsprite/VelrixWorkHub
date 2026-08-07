using FreeSql;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmsProjects;

public static class PmsProjectBaselineSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<PmsProjectBaselineRecord>();
        if (fsql.Select<PmsProjectBaselineRecord>().Any()) return;
        var project = fsql.Select<PmsProjectRecord>().OrderBy(x => x.CreatedTime).First();
        if (project is null) return;
        var item = new PmsProjectBaseline(project.Id, 1, "立项计划基线", DateTime.Now, DateOnly.FromDateTime(project.PlannedStart), DateOnly.FromDateTime(project.PlannedEnd), project.PercentComplete, checked((int)fsql.Select<PmsProjectPhaseRecord>().Where(x => x.ProjectId == project.Id).Count()), checked((int)fsql.Select<PmsWbsTaskRecord>().Where(x => x.ProjectId == project.Id).Count()));
        fsql.Insert(new PmsProjectBaselineRecord { Id = item.Id, ProjectId = item.ProjectId, VersionNumber = item.VersionNumber, Label = item.Label, SnapshotTime = item.SnapshotTime, PlannedStart = item.PlannedStart.ToDateTime(TimeOnly.MinValue), PlannedEnd = item.PlannedEnd.ToDateTime(TimeOnly.MinValue), PercentComplete = item.PercentComplete, PhaseCount = item.PhaseCount, TaskCount = item.TaskCount }).ExecuteAffrows();
    }
}
