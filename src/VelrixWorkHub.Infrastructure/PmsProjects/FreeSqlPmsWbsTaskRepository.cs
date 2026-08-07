using FreeSql;
using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmsProjects;

public sealed class FreeSqlPmsWbsTaskRepository(IFreeSql fsql) : IPmsWbsTaskRepository
{
    public IReadOnlyList<PmsWbsTask> List(Guid? projectId = null)
    {
        var query = fsql.Select<PmsWbsTaskRecord>(); if (projectId is not null) query = query.Where(x => x.ProjectId == projectId);
        return query.OrderBy(x => x.ProjectId).OrderBy(x => x.Sequence).ToList().Select(ToDomain).ToArray();
    }
    public void Add(PmsWbsTask item) { var now = DateTime.Now; fsql.Insert(ToRecord(item, now, now)).ExecuteAffrows(); }
    public void Update(PmsWbsTask item)
    {
        var rows = fsql.Update<PmsWbsTaskRecord>().Set(x => x.ParentId, item.ParentId).Set(x => x.Title, item.Title).Set(x => x.AssigneeName, item.AssigneeName).Set(x => x.Sequence, item.Sequence).Set(x => x.PlannedStart, item.PlannedStart.ToDateTime(TimeOnly.MinValue)).Set(x => x.PlannedEnd, item.PlannedEnd.ToDateTime(TimeOnly.MinValue)).Set(x => x.PercentComplete, item.PercentComplete).Set(x => x.IsMilestone, item.IsMilestone).Set(x => x.Status, item.Status).Set(x => x.ModifiedTime, DateTime.Now).Where(x => x.Id == item.Id).ExecuteAffrows(); if (rows == 0) throw new InvalidOperationException("WBS 任务不存在或已被删除。");
    }
    public void Remove(Guid id) => fsql.Delete<PmsWbsTaskRecord>().Where(x => x.Id == id).ExecuteAffrows();
    private static PmsWbsTask ToDomain(PmsWbsTaskRecord x) => PmsWbsTask.Restore(x.Id, x.ProjectId, x.ParentId, x.Title, x.AssigneeName, x.Sequence, DateOnly.FromDateTime(x.PlannedStart), DateOnly.FromDateTime(x.PlannedEnd), x.IsMilestone, x.PercentComplete, x.Status);
    private static PmsWbsTaskRecord ToRecord(PmsWbsTask x, DateTime created, DateTime modified) => new() { Id = x.Id, ProjectId = x.ProjectId, ParentId = x.ParentId, Title = x.Title, AssigneeName = x.AssigneeName, Sequence = x.Sequence, PlannedStart = x.PlannedStart.ToDateTime(TimeOnly.MinValue), PlannedEnd = x.PlannedEnd.ToDateTime(TimeOnly.MinValue), PercentComplete = x.PercentComplete, IsMilestone = x.IsMilestone, Status = x.Status, CreatedTime = created, ModifiedTime = modified };
}
