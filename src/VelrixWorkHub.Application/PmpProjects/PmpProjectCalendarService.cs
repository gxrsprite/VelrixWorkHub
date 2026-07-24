using AdminBlazor.Services;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmpProjects;

public interface IPmpProjectCalendarOverrideRepository
{
    IReadOnlyList<PmpProjectCalendarOverride> List(Guid projectId);
    void Add(PmpProjectCalendarOverride item);
    void Update(PmpProjectCalendarOverride item);
    void Remove(Guid id);
}

public sealed record PmpProjectCalendarDay(DateOnly Date, bool IsWorkingDay, string? Note, bool IsOverride);

public sealed class PmpProjectCalendarService(IPmpProjectCalendarOverrideRepository repository, IPmpProjectRepository projects, WorkingDayCalendar? baseCalendar = null)
{
    public IReadOnlyList<PmpProjectCalendarDay> List(Guid projectId, DateOnly start, DateOnly end)
    {
        var project = EnsureProject(projectId);
        if (end < start || end.DayNumber - start.DayNumber > 61) throw new ArgumentException("日历一次最多查看 62 天且结束日期不能早于开始日期。", nameof(end));
        var overrides = repository.List(projectId).ToDictionary(x => x.Date);
        return Enumerable.Range(0, end.DayNumber - start.DayNumber + 1).Select(offset =>
        {
            var date = start.AddDays(offset);
            if (overrides.TryGetValue(date, out var item)) return new PmpProjectCalendarDay(date, item.IsWorkingDay, item.Note, true);
            return new PmpProjectCalendarDay(date, baseCalendar?.IsWorkingDay(date.ToDateTime(TimeOnly.MinValue)) ?? (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday), null, false);
        }).ToArray();
    }
    public PmpProjectCalendarOverride Save(Guid projectId, DateOnly date, bool isWorkingDay, string? note)
    {
        var project = EnsureProject(projectId); EnsureWithinProject(project, date);
        var item = repository.List(projectId).FirstOrDefault(x => x.Date == date);
        if (item is null) { item = new PmpProjectCalendarOverride(projectId, date, isWorkingDay, note); repository.Add(item); }
        else { item.Edit(projectId, date, isWorkingDay, note); repository.Update(item); }
        return item;
    }
    public void Remove(PmpProjectCalendarOverride item) => repository.Remove(item.Id);
    public void Remove(Guid projectId, DateOnly date)
    {
        EnsureProject(projectId);
        var item = repository.List(projectId).FirstOrDefault(x => x.Date == date) ?? throw new InvalidOperationException("项目日历覆盖不存在或已被删除。");
        repository.Remove(item.Id);
    }
    private PmpProject EnsureProject(Guid projectId) => projects.List().FirstOrDefault(x => x.Id == projectId) ?? throw new InvalidOperationException("关联项目不存在。");
    private static void EnsureWithinProject(PmpProject project, DateOnly date) { if (date < project.PlannedStart || date > project.PlannedEnd) throw new InvalidOperationException("项目日历日期必须落在项目计划周期内。"); }
}
