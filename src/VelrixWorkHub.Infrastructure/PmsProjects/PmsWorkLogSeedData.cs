using FreeSql;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.PmsProjects;
public static class PmsWorkLogSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<PmsWorkLogRecord>(); if (fsql.Select<PmsWorkLogRecord>().Any()) return; var project = fsql.Select<PmsProjectRecord>().OrderBy(x => x.CreatedTime).First(); var task = fsql.Select<PmsWbsTaskRecord>().Where(x => x.ProjectId == project.Id).OrderBy(x => x.Sequence).First(); if (project is null) return;
        var item = new PmsWorkLog(project.Id, task?.Id, DateOnly.FromDateTime(DateTime.Today), "项目经理", 6.5m, "需求确认与方案评审准备"); fsql.Insert(new PmsWorkLogRecord { Id = item.Id, ProjectId = item.ProjectId, WbsTaskId = item.WbsTaskId, WorkDate = item.WorkDate.ToDateTime(TimeOnly.MinValue), MemberName = item.MemberName, Hours = item.Hours, Note = item.Note }).ExecuteAffrows();
    }
}
