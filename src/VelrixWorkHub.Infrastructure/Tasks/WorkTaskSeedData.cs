using FreeSql;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Tasks;

public static class WorkTaskSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<WorkTaskRecord>();
        if (fsql.Select<WorkTaskRecord>().Any()) return;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var now = DateTime.Now;
        fsql.Insert(new[]
        {
            New("完成 CRM 客户字段梳理", "确认客户名称、负责人和生命周期字段，整理成一页说明。", today, now),
            New("准备周一项目同步", "汇总上周进展和本周风险，提前发给项目成员。", today.AddDays(1), now),
            New("回访重点客户 Aster", "确认第二阶段报价反馈，并约定下一次沟通时间。", today.AddDays(2), now)
        }).ExecuteAffrows();
    }

    private static WorkTaskRecord New(string title, string description, DateOnly dueDate, DateTime now)
    {
        var task = new WorkTask(title, description, dueDate);
        return new WorkTaskRecord
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            DueDate = task.DueDate?.ToDateTime(TimeOnly.MinValue),
            CreatedTime = now,
            ModifiedTime = now
        };
    }
}
