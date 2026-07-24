using FreeSql;
using VelrixWorkHub.Application.Tasks;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Tasks;

public sealed class FreeSqlWorkTaskRepository(IFreeSql fsql) : IWorkTaskRepository
{
    public IReadOnlyList<WorkTask> List()
    {
        return fsql.Select<WorkTaskRecord>()
            .OrderByDescending(record => record.CreatedTime)
            .ToList()
            .Select(ToDomain)
            .ToArray();
    }

    public void Add(WorkTask task)
    {
        var now = DateTime.Now;
        fsql.Insert(ToRecord(task, now, now)).ExecuteAffrows();
    }

    public void Update(WorkTask task)
    {
        var updated = fsql.Update<WorkTaskRecord>()
            .Set(record => record.Title, task.Title)
            .Set(record => record.Description, task.Description)
            .Set(record => record.Status, task.Status)
            .Set(record => record.DueDate, (DateTime?)task.DueDate?.ToDateTime(TimeOnly.MinValue))
            .Set(record => record.ModifiedTime, DateTime.Now)
            .Where(record => record.Id == task.Id)
            .ExecuteAffrows();

        if (updated == 0)
            throw new InvalidOperationException("任务不存在或已被删除。");
    }

    public void Remove(Guid taskId)
    {
        fsql.Delete<WorkTaskRecord>().Where(record => record.Id == taskId).ExecuteAffrows();
    }

    private static WorkTask ToDomain(WorkTaskRecord record)
    {
        var task = new WorkTask(record.Title, record.Description, record.DueDate is null ? null : DateOnly.FromDateTime(record.DueDate.Value)) { Id = record.Id };
        if (record.Status == WorkTaskStatus.InProgress) task.Start();
        else if (record.Status == WorkTaskStatus.Done) task.Complete();
        return task;
    }

    private static WorkTaskRecord ToRecord(WorkTask task, DateTime createdTime, DateTime modifiedTime) => new()
    {
        Id = task.Id,
        Title = task.Title,
        Description = task.Description,
        Status = task.Status,
        DueDate = task.DueDate?.ToDateTime(TimeOnly.MinValue),
        CreatedTime = createdTime,
        ModifiedTime = modifiedTime
    };
}
