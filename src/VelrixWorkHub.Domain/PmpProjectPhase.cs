namespace VelrixWorkHub.Domain;

public enum PmpProjectPhaseKind { Phase, Milestone }
public enum PmpProjectPhaseStatus { Planned, Active, Completed, Cancelled }

public sealed class PmpProjectPhase
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public PmpProjectPhaseKind Kind { get; private set; }
    public int Sequence { get; private set; }
    public DateOnly PlannedStart { get; private set; }
    public DateOnly PlannedEnd { get; private set; }
    public int PercentComplete { get; private set; }
    public PmpProjectPhaseStatus Status { get; private set; }

    public PmpProjectPhase(Guid projectId, string name, PmpProjectPhaseKind kind, int sequence, DateOnly plannedStart, DateOnly plannedEnd)
    {
        Edit(projectId, name, kind, sequence, plannedStart, plannedEnd);
        Status = PmpProjectPhaseStatus.Planned;
    }

    public static PmpProjectPhase Restore(Guid id, Guid projectId, string name, PmpProjectPhaseKind kind, int sequence, DateOnly plannedStart, DateOnly plannedEnd, int percentComplete, PmpProjectPhaseStatus status)
    {
        var item = new PmpProjectPhase(projectId, name, kind, sequence, plannedStart, plannedEnd) { Id = id, PercentComplete = percentComplete, Status = status };
        if (percentComplete is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(percentComplete), "完成百分比必须在 0 到 100 之间。");
        return item;
    }

    public void Edit(Guid projectId, string name, PmpProjectPhaseKind kind, int sequence, DateOnly plannedStart, DateOnly plannedEnd)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("必须关联项目。", nameof(projectId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("阶段或里程碑名称不能为空。", nameof(name));
        if (sequence < 1) throw new ArgumentOutOfRangeException(nameof(sequence), "顺序必须大于 0。");
        if (plannedEnd < plannedStart) throw new ArgumentException("计划结束日期不能早于开始日期。", nameof(plannedEnd));
        if (kind == PmpProjectPhaseKind.Milestone && plannedEnd != plannedStart) throw new ArgumentException("里程碑的计划开始和结束日期必须相同。", nameof(plannedEnd));
        ProjectId = projectId; Name = name.Trim(); Kind = kind; Sequence = sequence; PlannedStart = plannedStart; PlannedEnd = plannedEnd;
    }

    public void SetStatus(PmpProjectPhaseStatus status)
    {
        if (status == Status) return;
        var allowed = (Status, status) switch
        {
            (PmpProjectPhaseStatus.Planned, PmpProjectPhaseStatus.Active) => true,
            (PmpProjectPhaseStatus.Planned, PmpProjectPhaseStatus.Cancelled) => true,
            (PmpProjectPhaseStatus.Active, PmpProjectPhaseStatus.Completed) => true,
            (PmpProjectPhaseStatus.Active, PmpProjectPhaseStatus.Cancelled) => true,
            _ => false
        };
        if (!allowed) throw new InvalidOperationException($"阶段不能从“{Status}”变更为“{status}”。");
        Status = status;
        if (status == PmpProjectPhaseStatus.Completed) PercentComplete = 100;
    }

    public void SetPercentComplete(int percent)
    {
        if (percent is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(percent), "完成百分比必须在 0 到 100 之间。");
        PercentComplete = percent;
        if (percent == 100) Status = PmpProjectPhaseStatus.Completed;
    }
}
