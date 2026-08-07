namespace VelrixWorkHub.Domain;

public enum PmsProjectPhaseKind { Phase, Milestone }
public enum PmsProjectPhaseStatus { Planned, Active, Completed, Cancelled }

public sealed class PmsProjectPhase
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public PmsProjectPhaseKind Kind { get; private set; }
    public int Sequence { get; private set; }
    public DateOnly PlannedStart { get; private set; }
    public DateOnly PlannedEnd { get; private set; }
    public int PercentComplete { get; private set; }
    public PmsProjectPhaseStatus Status { get; private set; }

    public PmsProjectPhase(Guid projectId, string name, PmsProjectPhaseKind kind, int sequence, DateOnly plannedStart, DateOnly plannedEnd)
    {
        Edit(projectId, name, kind, sequence, plannedStart, plannedEnd);
        Status = PmsProjectPhaseStatus.Planned;
    }

    public static PmsProjectPhase Restore(Guid id, Guid projectId, string name, PmsProjectPhaseKind kind, int sequence, DateOnly plannedStart, DateOnly plannedEnd, int percentComplete, PmsProjectPhaseStatus status)
    {
        var item = new PmsProjectPhase(projectId, name, kind, sequence, plannedStart, plannedEnd) { Id = id, PercentComplete = percentComplete, Status = status };
        if (percentComplete is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(percentComplete), "完成百分比必须在 0 到 100 之间。");
        return item;
    }

    public void Edit(Guid projectId, string name, PmsProjectPhaseKind kind, int sequence, DateOnly plannedStart, DateOnly plannedEnd)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("必须关联项目。", nameof(projectId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("阶段或里程碑名称不能为空。", nameof(name));
        if (sequence < 1) throw new ArgumentOutOfRangeException(nameof(sequence), "顺序必须大于 0。");
        if (plannedEnd < plannedStart) throw new ArgumentException("计划结束日期不能早于开始日期。", nameof(plannedEnd));
        if (kind == PmsProjectPhaseKind.Milestone && plannedEnd != plannedStart) throw new ArgumentException("里程碑的计划开始和结束日期必须相同。", nameof(plannedEnd));
        ProjectId = projectId; Name = name.Trim(); Kind = kind; Sequence = sequence; PlannedStart = plannedStart; PlannedEnd = plannedEnd;
    }

    public void SetStatus(PmsProjectPhaseStatus status)
    {
        if (status == Status) return;
        var allowed = (Status, status) switch
        {
            (PmsProjectPhaseStatus.Planned, PmsProjectPhaseStatus.Active) => true,
            (PmsProjectPhaseStatus.Planned, PmsProjectPhaseStatus.Cancelled) => true,
            (PmsProjectPhaseStatus.Active, PmsProjectPhaseStatus.Completed) => true,
            (PmsProjectPhaseStatus.Active, PmsProjectPhaseStatus.Cancelled) => true,
            _ => false
        };
        if (!allowed) throw new InvalidOperationException($"阶段不能从“{Status}”变更为“{status}”。");
        Status = status;
        if (status == PmsProjectPhaseStatus.Completed) PercentComplete = 100;
    }

    public void SetPercentComplete(int percent)
    {
        if (percent is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(percent), "完成百分比必须在 0 到 100 之间。");
        PercentComplete = percent;
        if (percent == 100) Status = PmsProjectPhaseStatus.Completed;
    }
}
