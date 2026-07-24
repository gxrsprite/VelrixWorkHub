namespace VelrixWorkHub.Domain;

public sealed class WorkSchedule
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? Location { get; private set; }
    public DateTime StartTime { get; private set; }
    public DateTime EndTime { get; private set; }
    public WorkSchedule(string title, DateTime startTime, DateTime endTime, string? description = null, string? location = null) => Edit(title, startTime, endTime, description, location);
    public void Edit(string title, DateTime startTime, DateTime endTime, string? description, string? location)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("日程标题不能为空。", nameof(title));
        if (endTime <= startTime) throw new ArgumentException("结束时间必须晚于开始时间。", nameof(endTime));
        Title = title.Trim(); StartTime = startTime; EndTime = endTime;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
    }
    public bool Overlaps(DateTime startTime, DateTime endTime) => StartTime < endTime && startTime < EndTime;
}
