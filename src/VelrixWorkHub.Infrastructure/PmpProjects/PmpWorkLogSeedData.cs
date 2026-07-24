using FreeSql;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.PmpProjects;
public static class PmpWorkLogSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<PmpWorkLogRecord>(); if (fsql.Select<PmpWorkLogRecord>().Any()) return; var project = fsql.Select<PmpProjectRecord>().OrderBy(x => x.CreatedTime).First(); var task = fsql.Select<PmpWbsTaskRecord>().Where(x => x.ProjectId == project.Id).OrderBy(x => x.Sequence).First(); if (project is null) return;
        var item = new PmpWorkLog(project.Id, task?.Id, DateOnly.FromDateTime(DateTime.Today), "项目经理", 6.5m, "需求确认与方案评审准备"); fsql.Insert(new PmpWorkLogRecord { Id = item.Id, ProjectId = item.ProjectId, WbsTaskId = item.WbsTaskId, WorkDate = item.WorkDate.ToDateTime(TimeOnly.MinValue), MemberName = item.MemberName, Hours = item.Hours, Note = item.Note }).ExecuteAffrows();
    }
}
