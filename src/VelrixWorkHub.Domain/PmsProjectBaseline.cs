namespace VelrixWorkHub.Domain;

public sealed class PmsProjectBaseline
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ProjectId { get; private set; }
    public int VersionNumber { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public DateTime SnapshotTime { get; private set; }
    public DateOnly PlannedStart { get; private set; }
    public DateOnly PlannedEnd { get; private set; }
    public int PercentComplete { get; private set; }
    public int PhaseCount { get; private set; }
    public int TaskCount { get; private set; }

    public PmsProjectBaseline(Guid projectId, int versionNumber, string label, DateTime snapshotTime, DateOnly plannedStart, DateOnly plannedEnd, int percentComplete, int phaseCount, int taskCount)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("项目不能为空。", nameof(projectId));
        if (versionNumber < 1) throw new ArgumentOutOfRangeException(nameof(versionNumber), "基线版本必须从 1 开始。");
        if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("基线名称不能为空。", nameof(label));
        if (plannedEnd < plannedStart) throw new ArgumentException("计划结束日期不能早于开始日期。", nameof(plannedEnd));
        if (percentComplete is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(percentComplete));
        if (phaseCount < 0) throw new ArgumentOutOfRangeException(nameof(phaseCount));
        if (taskCount < 0) throw new ArgumentOutOfRangeException(nameof(taskCount));
        ProjectId = projectId; VersionNumber = versionNumber; Label = label.Trim(); SnapshotTime = snapshotTime;
        PlannedStart = plannedStart; PlannedEnd = plannedEnd; PercentComplete = percentComplete;
        PhaseCount = phaseCount; TaskCount = taskCount;
    }
}
