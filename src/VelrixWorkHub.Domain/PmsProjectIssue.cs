namespace VelrixWorkHub.Domain;

public enum PmsProjectIssueKind { Risk, Issue }
public enum PmsProjectIssuePriority { Low, Medium, High, Critical }
public enum PmsProjectIssueStatus { Open, InProgress, Resolved, Closed }

public sealed class PmsProjectIssue
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ProjectId { get; private set; }
    public PmsProjectIssueKind Kind { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? OwnerName { get; private set; }
    public PmsProjectIssuePriority Priority { get; private set; }
    public PmsProjectIssueStatus Status { get; private set; }
    public DateOnly? DueDate { get; private set; }

    public PmsProjectIssue(Guid projectId, PmsProjectIssueKind kind, string title, string? description, string? ownerName, PmsProjectIssuePriority priority, DateOnly? dueDate)
    { Edit(projectId, kind, title, description, ownerName, priority, dueDate); Status = PmsProjectIssueStatus.Open; }

    public static PmsProjectIssue Restore(Guid id, Guid projectId, PmsProjectIssueKind kind, string title, string? description, string? ownerName, PmsProjectIssuePriority priority, DateOnly? dueDate, PmsProjectIssueStatus status)
        => new(projectId, kind, title, description, ownerName, priority, dueDate) { Id = id, Status = status };

    public void Edit(Guid projectId, PmsProjectIssueKind kind, string title, string? description, string? ownerName, PmsProjectIssuePriority priority, DateOnly? dueDate)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("必须关联项目。", nameof(projectId));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("风险或问题标题不能为空。", nameof(title));
        ProjectId = projectId; Kind = kind; Title = title.Trim(); Description = Clean(description); OwnerName = Clean(ownerName); Priority = priority; DueDate = dueDate;
    }
    public void SetStatus(PmsProjectIssueStatus status)
    {
        if (status == Status) return;
        var allowed = (Status, status) switch
        {
            (PmsProjectIssueStatus.Open, PmsProjectIssueStatus.InProgress) => true,
            (PmsProjectIssueStatus.Open, PmsProjectIssueStatus.Closed) => true,
            (PmsProjectIssueStatus.InProgress, PmsProjectIssueStatus.Resolved) => true,
            (PmsProjectIssueStatus.Resolved, PmsProjectIssueStatus.Closed) => true,
            _ => false
        };
        if (!allowed) throw new InvalidOperationException($"风险问题不能从“{Status}”变更为“{status}”。");
        Status = status;
    }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
