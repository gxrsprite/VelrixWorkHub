using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.Schedules;
public sealed class WorkScheduleService(IWorkScheduleRepository repository)
{
    public IReadOnlyList<WorkSchedule> List(ScheduleFilter filter = ScheduleFilter.All)
    {
        var now = DateTime.Now; var items = repository.List();
        return filter switch { ScheduleFilter.Upcoming => items.Where(item => item.EndTime >= now).ToArray(), ScheduleFilter.Past => items.Where(item => item.EndTime < now).ToArray(), _ => items };
    }
    public int Count(ScheduleFilter filter) => List(filter).Count;
    public WorkSchedule Create(string title, DateTime startTime, DateTime endTime, string? description, string? location)
    { EnsureNoConflict(startTime, endTime, null); var item = new WorkSchedule(title, startTime, endTime, description, location); repository.Add(item); return item; }
    public void Edit(WorkSchedule item, string title, DateTime startTime, DateTime endTime, string? description, string? location)
    { EnsureNoConflict(startTime, endTime, item.Id); item.Edit(title, startTime, endTime, description, location); repository.Update(item); }
    public void Remove(WorkSchedule item) => repository.Remove(item.Id);
    private void EnsureNoConflict(DateTime start, DateTime end, Guid? ignoredId)
    {
        if (end <= start) throw new ArgumentException("结束时间必须晚于开始时间。", nameof(end));
        if (repository.List().Any(item => item.Id != ignoredId && item.Overlaps(start, end))) throw new InvalidOperationException("该时间段与已有日程冲突，请调整时间后再保存。");
    }
}
