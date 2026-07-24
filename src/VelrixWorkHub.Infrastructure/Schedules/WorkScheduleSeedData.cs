using FreeSql;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Schedules;
public static class WorkScheduleSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<WorkScheduleRecord>(); if (fsql.Select<WorkScheduleRecord>().Any()) return;
        var start = DateTime.Today.AddDays(1).AddHours(10); var item = new WorkSchedule("周一项目同步", start, start.AddHours(1), "同步项目进展、风险和本周计划。", "会议室 A"); var now = DateTime.Now;
        fsql.Insert(new WorkScheduleRecord { Id = item.Id, Title = item.Title, Description = item.Description, Location = item.Location, StartTime = item.StartTime, EndTime = item.EndTime, CreatedTime = now, ModifiedTime = now }).ExecuteAffrows();
    }
}
