namespace VelrixWorkHub.Domain;

public enum WorkTaskStatus
{
    Todo,
    InProgress,
    Done
}

public sealed class WorkTask
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public WorkTaskStatus Status { get; private set; }
    public DateOnly? DueDate { get; private set; }

    public WorkTask(string title, string? description = null, DateOnly? dueDate = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("任务标题不能为空。", nameof(title));

        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DueDate = dueDate;
        Status = WorkTaskStatus.Todo;
    }

    public void Start()
    {
        if (Status == WorkTaskStatus.Done)
            throw new InvalidOperationException("已完成的任务不能直接开始。请先恢复任务。");

        Status = WorkTaskStatus.InProgress;
    }

    public void Complete() => Status = WorkTaskStatus.Done;

    public void Reopen() => Status = WorkTaskStatus.Todo;

    public void Edit(string title, string? description, DateOnly? dueDate)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("任务标题不能为空。", nameof(title));

        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DueDate = dueDate;
    }
}
