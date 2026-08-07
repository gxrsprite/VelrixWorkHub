using FreeSql;
using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmsProjects;

public sealed class FreeSqlPmsProjectBaselineRepository(IFreeSql fsql) : IPmsProjectBaselineRepository
{
    public IReadOnlyList<PmsProjectBaseline> List(Guid? projectId = null)
    {
        var query = fsql.Select<PmsProjectBaselineRecord>();
        if (projectId is not null) query = query.Where(x => x.ProjectId == projectId);
        return query.OrderByDescending(x => x.SnapshotTime).ToList().Select(ToDomain).ToArray();
    }

    public int NextVersion(Guid projectId) => (fsql.Select<PmsProjectBaselineRecord>().Where(x => x.ProjectId == projectId).Max(x => (int?)x.VersionNumber) ?? 0) + 1;

    public void Add(PmsProjectBaseline item) => fsql.Insert(new PmsProjectBaselineRecord { Id = item.Id, ProjectId = item.ProjectId, VersionNumber = item.VersionNumber, Label = item.Label, SnapshotTime = item.SnapshotTime, PlannedStart = item.PlannedStart.ToDateTime(TimeOnly.MinValue), PlannedEnd = item.PlannedEnd.ToDateTime(TimeOnly.MinValue), PercentComplete = item.PercentComplete, PhaseCount = item.PhaseCount, TaskCount = item.TaskCount }).ExecuteAffrows();

    private static PmsProjectBaseline ToDomain(PmsProjectBaselineRecord x) => new(x.ProjectId, x.VersionNumber, x.Label, x.SnapshotTime, DateOnly.FromDateTime(x.PlannedStart), DateOnly.FromDateTime(x.PlannedEnd), x.PercentComplete, x.PhaseCount, x.TaskCount) { Id = x.Id };
}
