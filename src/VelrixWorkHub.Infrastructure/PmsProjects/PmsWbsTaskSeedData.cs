using FreeSql;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmsProjects;

public static class PmsWbsTaskSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<PmsWbsTaskRecord>(); if (fsql.Select<PmsWbsTaskRecord>().Any()) return;
        var project = fsql.Select<PmsProjectRecord>().OrderBy(x => x.CreatedTime).First(); if (project is null) return;
        var start = DateOnly.FromDateTime(project.PlannedStart); var root = new PmsWbsTask(project.Id, null, "项目启动与需求确认", "项目经理", 1, start, start.AddDays(14), false); var child = new PmsWbsTask(project.Id, root.Id, "确认客户需求", "业务负责人", 1, start, start.AddDays(7), false); var now = DateTime.Now;
        fsql.Insert(new[] { root, child }.Select(x => new PmsWbsTaskRecord { Id = x.Id, ProjectId = x.ProjectId, ParentId = x.ParentId, Title = x.Title, AssigneeName = x.AssigneeName, Sequence = x.Sequence, PlannedStart = x.PlannedStart.ToDateTime(TimeOnly.MinValue), PlannedEnd = x.PlannedEnd.ToDateTime(TimeOnly.MinValue), PercentComplete = x.PercentComplete, IsMilestone = x.IsMilestone, Status = x.Status, CreatedTime = now, ModifiedTime = now })).ExecuteAffrows();
    }
}
