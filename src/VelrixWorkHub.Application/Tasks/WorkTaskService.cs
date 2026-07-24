using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Tasks;

public sealed class WorkTaskService(IWorkTaskRepository repository)
{
    public IReadOnlyList<WorkTask> List(WorkTaskFilter filter = WorkTaskFilter.All)
    {
        var tasks = repository.List();
        return filter switch
        {
            WorkTaskFilter.Open => tasks.Where(task => task.Status != WorkTaskStatus.Done).ToArray(),
            WorkTaskFilter.Done => tasks.Where(task => task.Status == WorkTaskStatus.Done).ToArray(),
            WorkTaskFilter.Overdue => tasks.Where(IsOverdue).ToArray(),
            _ => tasks
        };
    }

    public int Count(WorkTaskFilter filter) => List(filter).Count;

    private static bool IsOverdue(WorkTask task) =>
        task.DueDate < DateOnly.FromDateTime(DateTime.Today) && task.Status != WorkTaskStatus.Done;

    public WorkTask Create(string title, string? description = null, DateOnly? dueDate = null)
    {
        var task = new WorkTask(title, description, dueDate);
        repository.Add(task);
        return task;
    }

    public void Start(WorkTask task)
    {
        task.Start();
        repository.Update(task);
    }

    public void Edit(WorkTask task, string title, string? description, DateOnly? dueDate)
    {
        task.Edit(title, description, dueDate);
        repository.Update(task);
    }

    public void ToggleCompleted(WorkTask task)
    {
        if (task.Status == WorkTaskStatus.Done) task.Reopen();
        else task.Complete();

        repository.Update(task);
    }

    public void Remove(WorkTask task)
    {
        repository.Remove(task.Id);
    }
}
