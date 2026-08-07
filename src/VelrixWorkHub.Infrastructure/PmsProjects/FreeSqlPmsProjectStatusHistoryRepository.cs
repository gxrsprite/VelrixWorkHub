using FreeSql;
using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmsProjects;

public sealed class FreeSqlPmsProjectStatusHistoryRepository(IFreeSql fsql) : IPmsProjectStatusHistoryRepository
{
    public IReadOnlyList<PmsProjectStatusHistory> List(Guid projectId)
        => fsql.Select<PmsProjectStatusHistoryRecord>().Where(x => x.ProjectId == projectId).OrderByDescending(x => x.ChangedAt).ToList()
            .Select(x => new PmsProjectStatusHistory(x.ProjectId, x.FromStatus, x.ToStatus, x.Reason, x.ActorName, x.ChangedAt) { Id = x.Id }).ToArray();

    public void Add(PmsProjectStatusHistory history)
        => fsql.Insert(new PmsProjectStatusHistoryRecord { Id = history.Id, ProjectId = history.ProjectId, FromStatus = history.FromStatus, ToStatus = history.ToStatus, Reason = history.Reason, ActorName = history.ActorName, ChangedAt = history.ChangedAt }).ExecuteAffrows();
}
