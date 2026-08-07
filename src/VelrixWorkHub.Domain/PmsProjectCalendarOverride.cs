namespace VelrixWorkHub.Domain;

public sealed class PmsProjectCalendarOverride
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ProjectId { get; private set; }
    public DateOnly Date { get; private set; }
    public bool IsWorkingDay { get; private set; }
    public string? Note { get; private set; }

    public PmsProjectCalendarOverride(Guid projectId, DateOnly date, bool isWorkingDay, string? note = null) => Edit(projectId, date, isWorkingDay, note);
    public void Edit(Guid projectId, DateOnly date, bool isWorkingDay, string? note)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("必须关联项目。", nameof(projectId));
        if (note?.Trim().Length > 500) throw new ArgumentOutOfRangeException(nameof(note), "日历说明不能超过 500 个字符。");
        ProjectId = projectId; Date = date; IsWorkingDay = isWorkingDay; Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
