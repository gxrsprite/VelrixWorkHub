using FreeSql;
using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmsProjects;

public sealed class FreeSqlPmsProjectMemberRepository(IFreeSql fsql) : IPmsProjectMemberRepository
{
    public IReadOnlyList<PmsProjectMember> List(Guid? projectId = null)
    {
        var query = fsql.Select<PmsProjectMemberRecord>(); if (projectId is not null) query = query.Where(x => x.ProjectId == projectId);
        return query.OrderByDescending(x => x.IsPrimary).OrderBy(x => x.MemberName).ToList().Select(ToDomain).ToArray();
    }
    public void Add(PmsProjectMember item) { var now = DateTime.Now; fsql.Insert(ToRecord(item, now, now)).ExecuteAffrows(); }
    public void Update(PmsProjectMember item) { var rows = fsql.Update<PmsProjectMemberRecord>().Set(x => x.UserId, item.UserId).Set(x => x.MemberName, item.MemberName).Set(x => x.RoleName, item.RoleName).Set(x => x.IsPrimary, item.IsPrimary).Set(x => x.DepartmentName, item.DepartmentName).Set(x => x.ModifiedTime, DateTime.Now).Where(x => x.Id == item.Id).ExecuteAffrows(); if (rows == 0) throw new InvalidOperationException("项目成员不存在或已被删除。"); }
    public void Remove(Guid id) => fsql.Delete<PmsProjectMemberRecord>().Where(x => x.Id == id).ExecuteAffrows();
    private static PmsProjectMember ToDomain(PmsProjectMemberRecord x) { var item = new PmsProjectMember(x.ProjectId, x.MemberName, x.RoleName, x.IsPrimary, x.DepartmentName, x.UserId) { Id = x.Id }; return item; }
    private static PmsProjectMemberRecord ToRecord(PmsProjectMember x, DateTime created, DateTime modified) => new() { Id = x.Id, ProjectId = x.ProjectId, UserId = x.UserId, MemberName = x.MemberName, RoleName = x.RoleName, IsPrimary = x.IsPrimary, DepartmentName = x.DepartmentName, CreatedTime = created, ModifiedTime = modified };
}
