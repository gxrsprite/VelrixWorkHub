using FreeSql;
using VelrixWorkHub.Application.PmpProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmpProjects;

public sealed class FreeSqlPmpProjectStatusHistoryRepository(IFreeSql fsql) : IPmpProjectStatusHistoryRepository
{
    public IReadOnlyList<PmpProjectStatusHistory> List(Guid projectId)
        => fsql.Select<PmpProjectStatusHistoryRecord>().Where(x => x.ProjectId == projectId).OrderByDescending(x => x.ChangedAt).ToList()
            .Select(x => new PmpProjectStatusHistory(x.ProjectId, x.FromStatus, x.ToStatus, x.Reason, x.ActorName, x.ChangedAt) { Id = x.Id }).ToArray();

    public void Add(PmpProjectStatusHistory history)
        => fsql.Insert(new PmpProjectStatusHistoryRecord { Id = history.Id, ProjectId = history.ProjectId, FromStatus = history.FromStatus, ToStatus = history.ToStatus, Reason = history.Reason, ActorName = history.ActorName, ChangedAt = history.ChangedAt }).ExecuteAffrows();
}
