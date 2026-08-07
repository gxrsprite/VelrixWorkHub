using FreeSql;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmsProjects;

public static class PmsProjectMemberSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<PmsProjectMemberRecord>(); if (fsql.Select<PmsProjectMemberRecord>().Any()) return;
        var project = fsql.Select<PmsProjectRecord>().OrderBy(x => x.CreatedTime).First(); if (project is null) return; var manager = new PmsProjectMember(project.Id, "项目经理", "项目经理", true); var owner = new PmsProjectMember(project.Id, "业务负责人", "业务负责人"); var now = DateTime.Now;
        fsql.Insert(new[] { manager, owner }.Select(x => new PmsProjectMemberRecord { Id = x.Id, ProjectId = x.ProjectId, MemberName = x.MemberName, RoleName = x.RoleName, IsPrimary = x.IsPrimary, CreatedTime = now, ModifiedTime = now })).ExecuteAffrows();
    }
}
