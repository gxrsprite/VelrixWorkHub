using FreeSql;
using VelrixWorkHub.Application.PmpProjects;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.PmpProjects;
public sealed class FreeSqlPmpProjectChangeRepository(IFreeSql fsql) : IPmpProjectChangeRepository
{
    public IReadOnlyList<PmpProjectChange> List(Guid? projectId = null) { var query = fsql.Select<PmpProjectChangeRecord>(); if (projectId is not null) query = query.Where(x => x.ProjectId == projectId); return query.OrderByDescending(x => x.CreatedTime).ToList().Select(ToDomain).ToArray(); }
    public void Add(PmpProjectChange item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(PmpProjectChange item) { var rows = fsql.Update<PmpProjectChangeRecord>().Set(x => x.Status, item.Status).Where(x => x.Id == item.Id).ExecuteAffrows(); if (rows == 0) throw new InvalidOperationException("变更记录不存在或已被删除。"); }
    private static PmpProjectChange ToDomain(PmpProjectChangeRecord x) { var item = new PmpProjectChange(x.ProjectId, x.Title, x.Reason, x.Impact, x.RequesterName, x.CreatedTime) { Id = x.Id }; item.SetStatus(x.Status); return item; }
    private static PmpProjectChangeRecord ToRecord(PmpProjectChange x) => new() { Id = x.Id, ProjectId = x.ProjectId, Title = x.Title, Reason = x.Reason, Impact = x.Impact, RequesterName = x.RequesterName, Status = x.Status, CreatedTime = x.CreatedTime };
}
