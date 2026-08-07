namespace VelrixWorkHub.Domain;

public enum PmsWbsTaskStatus { Todo, InProgress, Done }

public sealed class PmsWbsTask
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ProjectId { get; private set; }
    public Guid? ParentId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? AssigneeName { get; private set; }
    public int Sequence { get; private set; }
    public DateOnly PlannedStart { get; private set; }
    public DateOnly PlannedEnd { get; private set; }
    public int PercentComplete { get; private set; }
    public bool IsMilestone { get; private set; }
    public PmsWbsTaskStatus Status { get; private set; }

    public PmsWbsTask(Guid projectId, Guid? parentId, string title, string? assigneeName, int sequence, DateOnly plannedStart, DateOnly plannedEnd, bool isMilestone)
    { Edit(projectId, parentId, title, assigneeName, sequence, plannedStart, plannedEnd, isMilestone); Status = PmsWbsTaskStatus.Todo; }

    public static PmsWbsTask Restore(Guid id, Guid projectId, Guid? parentId, string title, string? assigneeName, int sequence, DateOnly plannedStart, DateOnly plannedEnd, bool isMilestone, int percentComplete, PmsWbsTaskStatus status)
    {
        var item = new PmsWbsTask(projectId, parentId, title, assigneeName, sequence, plannedStart, plannedEnd, isMilestone) { Id = id, PercentComplete = percentComplete, Status = status };
        if (percentComplete is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(percentComplete), "完成百分比必须在 0 到 100 之间。");
        return item;
    }

    public void Edit(Guid projectId, Guid? parentId, string title, string? assigneeName, int sequence, DateOnly plannedStart, DateOnly plannedEnd, bool isMilestone)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("必须关联项目。", nameof(projectId));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("任务名称不能为空。", nameof(title));
        if (sequence < 1) throw new ArgumentOutOfRangeException(nameof(sequence), "顺序必须大于 0。");
        if (plannedEnd < plannedStart) throw new ArgumentException("计划结束日期不能早于开始日期。", nameof(plannedEnd));
        if (isMilestone && plannedEnd != plannedStart) throw new ArgumentException("里程碑的计划开始和结束日期必须相同。", nameof(plannedEnd));
        ProjectId = projectId; ParentId = parentId; Title = title.Trim(); AssigneeName = string.IsNullOrWhiteSpace(assigneeName) ? null : assigneeName.Trim(); Sequence = sequence; PlannedStart = plannedStart; PlannedEnd = plannedEnd; IsMilestone = isMilestone;
    }

    public void SetStatus(PmsWbsTaskStatus status)
    {
        if (status == Status) return;
        var allowed = (Status, status) switch
        {
            (PmsWbsTaskStatus.Todo, PmsWbsTaskStatus.InProgress) => true,
            (PmsWbsTaskStatus.InProgress, PmsWbsTaskStatus.Done) => true,
            _ => false
        };
        if (!allowed) throw new InvalidOperationException($"WBS 任务不能从“{Status}”变更为“{status}”。");
        Status = status;
        if (status == PmsWbsTaskStatus.Done) PercentComplete = 100;
    }
    public void SetPercentComplete(int percent)
    {
        if (percent is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(percent), "完成百分比必须在 0 到 100 之间。");
        PercentComplete = percent;
        if (percent == 100) Status = PmsWbsTaskStatus.Done;
    }
}
