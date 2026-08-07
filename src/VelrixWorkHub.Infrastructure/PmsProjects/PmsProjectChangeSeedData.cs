using FreeSql;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.PmsProjects;
public static class PmsProjectChangeSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<PmsProjectChangeRecord>(); if (fsql.Select<PmsProjectChangeRecord>().Any()) return; var project = fsql.Select<PmsProjectRecord>().OrderBy(x => x.CreatedTime).First(); if (project is null) return;
        var item = new PmsProjectChange(project.Id, "补充接口联调范围", "客户新增两个外部系统接口，需要调整联调计划。", "预计增加 5 个工作日，需同步更新 WBS。", "业务负责人", DateTime.Now); var record = new PmsProjectChangeRecord { Id = item.Id, ProjectId = item.ProjectId, Title = item.Title, Reason = item.Reason, Impact = item.Impact, RequesterName = item.RequesterName, Status = item.Status, CreatedTime = item.CreatedTime }; fsql.Insert(record).ExecuteAffrows();
    }
}
