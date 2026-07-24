namespace VelrixWorkHub.Domain;

public enum PmpProjectIssueKind { Risk, Issue }
public enum PmpProjectIssuePriority { Low, Medium, High, Critical }
public enum PmpProjectIssueStatus { Open, InProgress, Resolved, Closed }

public sealed class PmpProjectIssue
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ProjectId { get; private set; }
    public PmpProjectIssueKind Kind { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? OwnerName { get; private set; }
    public PmpProjectIssuePriority Priority { get; private set; }
    public PmpProjectIssueStatus Status { get; private set; }
    public DateOnly? DueDate { get; private set; }

    public PmpProjectIssue(Guid projectId, PmpProjectIssueKind kind, string title, string? description, string? ownerName, PmpProjectIssuePriority priority, DateOnly? dueDate)
    { Edit(projectId, kind, title, description, ownerName, priority, dueDate); Status = PmpProjectIssueStatus.Open; }

    public static PmpProjectIssue Restore(Guid id, Guid projectId, PmpProjectIssueKind kind, string title, string? description, string? ownerName, PmpProjectIssuePriority priority, DateOnly? dueDate, PmpProjectIssueStatus status)
        => new(projectId, kind, title, description, ownerName, priority, dueDate) { Id = id, Status = status };

    public void Edit(Guid projectId, PmpProjectIssueKind kind, string title, string? description, string? ownerName, PmpProjectIssuePriority priority, DateOnly? dueDate)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("必须关联项目。", nameof(projectId));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("风险或问题标题不能为空。", nameof(title));
        ProjectId = projectId; Kind = kind; Title = title.Trim(); Description = Clean(description); OwnerName = Clean(ownerName); Priority = priority; DueDate = dueDate;
    }
    public void SetStatus(PmpProjectIssueStatus status)
    {
        if (status == Status) return;
        var allowed = (Status, status) switch
        {
            (PmpProjectIssueStatus.Open, PmpProjectIssueStatus.InProgress) => true,
            (PmpProjectIssueStatus.Open, PmpProjectIssueStatus.Closed) => true,
            (PmpProjectIssueStatus.InProgress, PmpProjectIssueStatus.Resolved) => true,
            (PmpProjectIssueStatus.Resolved, PmpProjectIssueStatus.Closed) => true,
            _ => false
        };
        if (!allowed) throw new InvalidOperationException($"风险问题不能从“{Status}”变更为“{status}”。");
        Status = status;
    }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
