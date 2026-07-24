using VelrixWorkHub.Application.Schedules;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class WorkScheduleTests
{
    [Fact]
    public void InvalidTimeRange_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new WorkSchedule("评审", DateTime.Today.AddHours(11), DateTime.Today.AddHours(10)));
    }

    [Fact]
    public void ServiceRejectsOverlappingSchedules()
    {
        var repository = new TestRepository();
        var service = new WorkScheduleService(repository);
        var start = DateTime.Today.AddHours(10);
        service.Create("第一次会议", start, start.AddHours(1), null, null);

        Assert.Throws<InvalidOperationException>(() => service.Create("冲突会议", start.AddMinutes(30), start.AddHours(2), null, null));
    }

    [Fact]
    public void ServiceAllowsEditingSameScheduleWithoutSelfConflict()
    {
        var repository = new TestRepository();
        var service = new WorkScheduleService(repository);
        var start = DateTime.Today.AddHours(10);
        var item = service.Create("第一次会议", start, start.AddHours(1), null, null);

        service.Edit(item, "已调整会议", start.AddHours(1), start.AddHours(2), "新备注", "会议室 B");

        Assert.Equal("已调整会议", item.Title);
        Assert.Equal("会议室 B", item.Location);
        Assert.Equal(1, repository.UpdatedCount);
    }

    private sealed class TestRepository : IWorkScheduleRepository
    {
        private readonly List<WorkSchedule> items = [];
        public int UpdatedCount { get; private set; }
        public IReadOnlyList<WorkSchedule> List() => items;
        public void Add(WorkSchedule schedule) => items.Add(schedule);
        public void Update(WorkSchedule schedule) => UpdatedCount++;
        public void Remove(Guid scheduleId) => items.RemoveAll(item => item.Id == scheduleId);
    }
}
