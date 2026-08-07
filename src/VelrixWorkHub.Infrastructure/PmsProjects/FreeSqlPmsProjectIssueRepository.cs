using FreeSql;
using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.PmsProjects;
public sealed class FreeSqlPmsProjectIssueRepository(IFreeSql fsql) : IPmsProjectIssueRepository
{
    public IReadOnlyList<PmsProjectIssue> List(Guid? projectId = null) { var query = fsql.Select<PmsProjectIssueRecord>(); if (projectId is not null) query = query.Where(x => x.ProjectId == projectId); return query.OrderBy(x => x.Status).OrderByDescending(x => x.Priority).ToList().Select(ToDomain).ToArray(); }
    public void Add(PmsProjectIssue item) { var now = DateTime.Now; fsql.Insert(ToRecord(item, now, now)).ExecuteAffrows(); }
    public void Update(PmsProjectIssue item) { var rows = fsql.Update<PmsProjectIssueRecord>().Set(x => x.Kind, item.Kind).Set(x => x.Title, item.Title).Set(x => x.Description, item.Description).Set(x => x.OwnerName, item.OwnerName).Set(x => x.Priority, item.Priority).Set(x => x.Status, item.Status).Set(x => x.DueDate, item.DueDate?.ToDateTime(TimeOnly.MinValue)).Set(x => x.ModifiedTime, DateTime.Now).Where(x => x.Id == item.Id).ExecuteAffrows(); if (rows == 0) throw new InvalidOperationException("风险或问题不存在或已被删除。"); }
    public void Remove(Guid id) => fsql.Delete<PmsProjectIssueRecord>().Where(x => x.Id == id).ExecuteAffrows();
    private static PmsProjectIssue ToDomain(PmsProjectIssueRecord x) => PmsProjectIssue.Restore(x.Id, x.ProjectId, x.Kind, x.Title, x.Description, x.OwnerName, x.Priority, x.DueDate is null ? null : DateOnly.FromDateTime(x.DueDate.Value), x.Status);
    private static PmsProjectIssueRecord ToRecord(PmsProjectIssue x, DateTime created, DateTime modified) => new() { Id = x.Id, ProjectId = x.ProjectId, Kind = x.Kind, Title = x.Title, Description = x.Description, OwnerName = x.OwnerName, Priority = x.Priority, Status = x.Status, DueDate = x.DueDate?.ToDateTime(TimeOnly.MinValue), CreatedTime = created, ModifiedTime = modified };
}
