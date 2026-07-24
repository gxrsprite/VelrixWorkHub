using FreeSql;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmpProjects;

public static class PmpProjectMemberSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<PmpProjectMemberRecord>(); if (fsql.Select<PmpProjectMemberRecord>().Any()) return;
        var project = fsql.Select<PmpProjectRecord>().OrderBy(x => x.CreatedTime).First(); if (project is null) return; var manager = new PmpProjectMember(project.Id, "项目经理", "项目经理", true); var owner = new PmpProjectMember(project.Id, "业务负责人", "业务负责人"); var now = DateTime.Now;
        fsql.Insert(new[] { manager, owner }.Select(x => new PmpProjectMemberRecord { Id = x.Id, ProjectId = x.ProjectId, MemberName = x.MemberName, RoleName = x.RoleName, IsPrimary = x.IsPrimary, CreatedTime = now, ModifiedTime = now })).ExecuteAffrows();
    }
}
