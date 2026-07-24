using FreeSql;
using VelrixWorkHub.Application.PmpProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmpProjects;

public sealed class FreeSqlPmpProjectMemberRepository(IFreeSql fsql) : IPmpProjectMemberRepository
{
    public IReadOnlyList<PmpProjectMember> List(Guid? projectId = null)
    {
        var query = fsql.Select<PmpProjectMemberRecord>(); if (projectId is not null) query = query.Where(x => x.ProjectId == projectId);
        return query.OrderByDescending(x => x.IsPrimary).OrderBy(x => x.MemberName).ToList().Select(ToDomain).ToArray();
    }
    public void Add(PmpProjectMember item) { var now = DateTime.Now; fsql.Insert(ToRecord(item, now, now)).ExecuteAffrows(); }
    public void Update(PmpProjectMember item) { var rows = fsql.Update<PmpProjectMemberRecord>().Set(x => x.UserId, item.UserId).Set(x => x.MemberName, item.MemberName).Set(x => x.RoleName, item.RoleName).Set(x => x.IsPrimary, item.IsPrimary).Set(x => x.DepartmentName, item.DepartmentName).Set(x => x.ModifiedTime, DateTime.Now).Where(x => x.Id == item.Id).ExecuteAffrows(); if (rows == 0) throw new InvalidOperationException("项目成员不存在或已被删除。"); }
    public void Remove(Guid id) => fsql.Delete<PmpProjectMemberRecord>().Where(x => x.Id == id).ExecuteAffrows();
    private static PmpProjectMember ToDomain(PmpProjectMemberRecord x) { var item = new PmpProjectMember(x.ProjectId, x.MemberName, x.RoleName, x.IsPrimary, x.DepartmentName, x.UserId) { Id = x.Id }; return item; }
    private static PmpProjectMemberRecord ToRecord(PmpProjectMember x, DateTime created, DateTime modified) => new() { Id = x.Id, ProjectId = x.ProjectId, UserId = x.UserId, MemberName = x.MemberName, RoleName = x.RoleName, IsPrimary = x.IsPrimary, DepartmentName = x.DepartmentName, CreatedTime = created, ModifiedTime = modified };
}
