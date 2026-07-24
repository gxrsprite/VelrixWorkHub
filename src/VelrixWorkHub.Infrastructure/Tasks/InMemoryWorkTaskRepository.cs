using VelrixWorkHub.Application.Tasks;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Tasks;

public sealed class InMemoryWorkTaskRepository : IWorkTaskRepository
{
    private readonly List<WorkTask> tasks =
    [
        new("完成 CRM 客户字段梳理", "确认客户名称、负责人和生命周期字段，整理成一页说明。", DateOnly.FromDateTime(DateTime.Today)),
        new("准备周一项目同步", "汇总上周进展和本周风险，提前发给项目成员。", DateOnly.FromDateTime(DateTime.Today.AddDays(1))),
        new("回访重点客户 Aster", "确认第二阶段报价反馈，并约定下一次沟通时间。", DateOnly.FromDateTime(DateTime.Today.AddDays(2)))
    ];

    public IReadOnlyList<WorkTask> List() => tasks;

    public void Add(WorkTask task) => tasks.Insert(0, task);

    public void Update(WorkTask task)
    {
        if (tasks.All(item => item.Id != task.Id))
            throw new InvalidOperationException("任务不存在或已被删除。");
    }

    public void Remove(Guid taskId) => tasks.RemoveAll(item => item.Id == taskId);
}
