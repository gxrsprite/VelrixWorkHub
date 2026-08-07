using FreeSql;
using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.PmsProjects;
public sealed class FreeSqlPmsProjectChangeRepository(IFreeSql fsql) : IPmsProjectChangeRepository
{
    public IReadOnlyList<PmsProjectChange> List(Guid? projectId = null) { var query = fsql.Select<PmsProjectChangeRecord>(); if (projectId is not null) query = query.Where(x => x.ProjectId == projectId); return query.OrderByDescending(x => x.CreatedTime).ToList().Select(ToDomain).ToArray(); }
    public void Add(PmsProjectChange item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(PmsProjectChange item) { var rows = fsql.Update<PmsProjectChangeRecord>().Set(x => x.Status, item.Status).Where(x => x.Id == item.Id).ExecuteAffrows(); if (rows == 0) throw new InvalidOperationException("变更记录不存在或已被删除。"); }
    private static PmsProjectChange ToDomain(PmsProjectChangeRecord x) { var item = new PmsProjectChange(x.ProjectId, x.Title, x.Reason, x.Impact, x.RequesterName, x.CreatedTime) { Id = x.Id }; item.SetStatus(x.Status); return item; }
    private static PmsProjectChangeRecord ToRecord(PmsProjectChange x) => new() { Id = x.Id, ProjectId = x.ProjectId, Title = x.Title, Reason = x.Reason, Impact = x.Impact, RequesterName = x.RequesterName, Status = x.Status, CreatedTime = x.CreatedTime };
}
