using FreeSql;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.PmpProjects;
public static class PmpProjectChangeSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<PmpProjectChangeRecord>(); if (fsql.Select<PmpProjectChangeRecord>().Any()) return; var project = fsql.Select<PmpProjectRecord>().OrderBy(x => x.CreatedTime).First(); if (project is null) return;
        var item = new PmpProjectChange(project.Id, "补充接口联调范围", "客户新增两个外部系统接口，需要调整联调计划。", "预计增加 5 个工作日，需同步更新 WBS。", "业务负责人", DateTime.Now); var record = new PmpProjectChangeRecord { Id = item.Id, ProjectId = item.ProjectId, Title = item.Title, Reason = item.Reason, Impact = item.Impact, RequesterName = item.RequesterName, Status = item.Status, CreatedTime = item.CreatedTime }; fsql.Insert(record).ExecuteAffrows();
    }
}
