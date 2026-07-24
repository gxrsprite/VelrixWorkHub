using FreeSql;
using VelrixWorkHub.Application.PmpProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmpProjects;

public sealed class FreeSqlPmpProjectWorkItemActivityRepository(IFreeSql fsql) : IPmpProjectWorkItemActivityRepository
{
    public IReadOnlyList<PmpProjectWorkItemActivity> List(Guid workItemId) => fsql.Select<PmpProjectWorkItemActivityRecord>().Where(x => x.WorkItemId == workItemId).OrderByDescending(x => x.OccurredAt).ToList().Select(x => PmpProjectWorkItemActivity.Restore(x.Id, x.WorkItemId, x.Type, x.Content, x.ActorName, x.PreviousStatus, x.CurrentStatus, x.OccurredAt)).ToArray();
    public void Add(PmpProjectWorkItemActivity activity) => fsql.Insert(new PmpProjectWorkItemActivityRecord { Id = activity.Id, WorkItemId = activity.WorkItemId, Type = activity.Type, Content = activity.Content, ActorName = activity.ActorName, PreviousStatus = activity.PreviousStatus, CurrentStatus = activity.CurrentStatus, OccurredAt = activity.OccurredAt }).ExecuteAffrows();
}
