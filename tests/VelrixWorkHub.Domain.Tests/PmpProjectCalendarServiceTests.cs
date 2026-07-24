using AdminBlazor.Services;
using VelrixWorkHub.Application.PmpProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PmpProjectCalendarServiceTests
{
    [Fact]
    public void Save_OverridesBaseCalendarOnlyWithinProjectDates()
    {
        var project = new PmpProject("PRJ-CALENDAR", "日历项目", null, null, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));
        var repository = new CalendarRepository();
        var calendar = new WorkingDayCalendar();
        var service = new PmpProjectCalendarService(repository, new ProjectRepository(project), calendar);
        var saturday = new DateOnly(2026, 7, 4);

        Assert.False(Assert.Single(service.List(project.Id, saturday, saturday)).IsWorkingDay);
        service.Save(project.Id, saturday, true, "项目冲刺补班");

        var overridden = Assert.Single(service.List(project.Id, saturday, saturday));
        Assert.True(overridden.IsWorkingDay); Assert.True(overridden.IsOverride); Assert.Equal("项目冲刺补班", overridden.Note);
        Assert.Throws<InvalidOperationException>(() => service.Save(project.Id, new DateOnly(2026, 8, 1), false, null));
    }

    [Fact]
    public void Save_SameDateUpdatesAndRemoveRestoresBaseCalendar()
    {
        var project = new PmpProject("PRJ-CALENDAR-REMOVE", "日历删除项目", null, null, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));
        var repository = new CalendarRepository();
        var service = new PmpProjectCalendarService(repository, new ProjectRepository(project), new WorkingDayCalendar());
        var saturday = new DateOnly(2026, 7, 4);

        service.Save(project.Id, saturday, true, "补班");
        service.Save(project.Id, saturday, false, "恢复休息");

        var updated = Assert.Single(service.List(project.Id, saturday, saturday));
        Assert.True(updated.IsOverride);
        Assert.False(updated.IsWorkingDay);
        Assert.Equal("恢复休息", updated.Note);
        Assert.Single(repository.List(project.Id));

        service.Remove(project.Id, saturday);

        var restored = Assert.Single(service.List(project.Id, saturday, saturday));
        Assert.False(restored.IsOverride);
        Assert.False(restored.IsWorkingDay);
        Assert.Null(restored.Note);
        Assert.Empty(repository.List(project.Id));
        Assert.Throws<InvalidOperationException>(() => service.Remove(project.Id, saturday));
    }
    private sealed class CalendarRepository : IPmpProjectCalendarOverrideRepository { private readonly List<PmpProjectCalendarOverride> data = []; public IReadOnlyList<PmpProjectCalendarOverride> List(Guid projectId) => data.Where(x => x.ProjectId == projectId).ToArray(); public void Add(PmpProjectCalendarOverride item) => data.Add(item); public void Update(PmpProjectCalendarOverride item) { } public void Remove(Guid id) => data.RemoveAll(x => x.Id == id); }
    private sealed class ProjectRepository(params PmpProject[] data) : IPmpProjectRepository { public IReadOnlyList<PmpProject> List() => data; public void Add(PmpProject item) { } public void Update(PmpProject item) { } public void Remove(Guid id) { } }
}
