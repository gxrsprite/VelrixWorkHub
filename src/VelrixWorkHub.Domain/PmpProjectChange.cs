namespace VelrixWorkHub.Domain;

public enum PmpProjectChangeStatus { Proposed, Approved, Rejected, Applied }

public sealed class PmpProjectChange
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ProjectId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public string? Impact { get; private set; }
    public string? RequesterName { get; private set; }
    public PmpProjectChangeStatus Status { get; private set; }
    public DateTime CreatedTime { get; private set; }

    public PmpProjectChange(Guid projectId, string title, string reason, string? impact, string? requesterName, DateTime createdTime)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("项目不能为空。", nameof(projectId));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("变更标题不能为空。", nameof(title));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("变更原因不能为空。", nameof(reason));
        ProjectId = projectId; Title = title.Trim(); Reason = reason.Trim(); Impact = string.IsNullOrWhiteSpace(impact) ? null : impact.Trim(); RequesterName = string.IsNullOrWhiteSpace(requesterName) ? null : requesterName.Trim(); CreatedTime = createdTime; Status = PmpProjectChangeStatus.Proposed;
    }

    public void SetStatus(PmpProjectChangeStatus status) => Status = status;
}
