using FreeSql;
using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmsProjects;

public sealed class FreeSqlPmsProjectWorkItemActivityRepository(IFreeSql fsql) : IPmsProjectWorkItemActivityRepository
{
    public IReadOnlyList<PmsProjectWorkItemActivity> List(Guid workItemId) => fsql.Select<PmsProjectWorkItemActivityRecord>().Where(x => x.WorkItemId == workItemId).OrderByDescending(x => x.OccurredAt).ToList().Select(x => PmsProjectWorkItemActivity.Restore(x.Id, x.WorkItemId, x.Type, x.Content, x.ActorName, x.PreviousStatus, x.CurrentStatus, x.OccurredAt)).ToArray();
    public void Add(PmsProjectWorkItemActivity activity) => fsql.Insert(new PmsProjectWorkItemActivityRecord { Id = activity.Id, WorkItemId = activity.WorkItemId, Type = activity.Type, Content = activity.Content, ActorName = activity.ActorName, PreviousStatus = activity.PreviousStatus, CurrentStatus = activity.CurrentStatus, OccurredAt = activity.OccurredAt }).ExecuteAffrows();
}
