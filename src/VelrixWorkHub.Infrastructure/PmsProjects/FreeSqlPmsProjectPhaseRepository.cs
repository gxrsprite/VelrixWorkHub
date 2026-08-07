using FreeSql;
using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmsProjects;

public sealed class FreeSqlPmsProjectPhaseRepository(IFreeSql fsql) : IPmsProjectPhaseRepository
{
    public IReadOnlyList<PmsProjectPhase> List(Guid? projectId = null)
    {
        var query = fsql.Select<PmsProjectPhaseRecord>();
        if (projectId is not null) query = query.Where(x => x.ProjectId == projectId);
        return query.OrderBy(x => x.ProjectId).OrderBy(x => x.Sequence).ToList().Select(ToDomain).ToArray();
    }

    public void Add(PmsProjectPhase item) { var now = DateTime.Now; fsql.Insert(ToRecord(item, now, now)).ExecuteAffrows(); }

    public void Update(PmsProjectPhase item)
    {
        var rows = fsql.Update<PmsProjectPhaseRecord>().Set(x => x.Name, item.Name).Set(x => x.Kind, item.Kind).Set(x => x.Sequence, item.Sequence).Set(x => x.PlannedStart, item.PlannedStart.ToDateTime(TimeOnly.MinValue)).Set(x => x.PlannedEnd, item.PlannedEnd.ToDateTime(TimeOnly.MinValue)).Set(x => x.PercentComplete, item.PercentComplete).Set(x => x.Status, item.Status).Set(x => x.ModifiedTime, DateTime.Now).Where(x => x.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("阶段或里程碑不存在或已被删除。");
    }

    public void Remove(Guid id) => fsql.Delete<PmsProjectPhaseRecord>().Where(x => x.Id == id).ExecuteAffrows();

    private static PmsProjectPhase ToDomain(PmsProjectPhaseRecord x)
        => PmsProjectPhase.Restore(x.Id, x.ProjectId, x.Name, x.Kind, x.Sequence, DateOnly.FromDateTime(x.PlannedStart), DateOnly.FromDateTime(x.PlannedEnd), x.PercentComplete, x.Status);

    private static PmsProjectPhaseRecord ToRecord(PmsProjectPhase x, DateTime created, DateTime modified) => new() { Id = x.Id, ProjectId = x.ProjectId, Name = x.Name, Kind = x.Kind, Sequence = x.Sequence, PlannedStart = x.PlannedStart.ToDateTime(TimeOnly.MinValue), PlannedEnd = x.PlannedEnd.ToDateTime(TimeOnly.MinValue), PercentComplete = x.PercentComplete, Status = x.Status, CreatedTime = created, ModifiedTime = modified };
}
