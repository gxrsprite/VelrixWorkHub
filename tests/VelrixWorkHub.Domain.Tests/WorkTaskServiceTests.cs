using VelrixWorkHub.Application.Tasks;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public class WorkTaskServiceTests
{
    [Fact]
    public void CreateAndToggle_UsesRepositoryBoundary()
    {
        var repository = new TestRepository();
        var service = new WorkTaskService(repository);

        var task = service.Create("整理合同");
        service.Start(task);
        service.ToggleCompleted(task);
        service.ToggleCompleted(task);

        Assert.Equal(WorkTaskStatus.Todo, task.Status);
        Assert.Equal(1, repository.AddedCount);
        Assert.Equal(3, repository.UpdatedCount);
    }

    [Fact]
    public void OverdueFilter_ExcludesCompletedTasks()
    {
        var repository = new TestRepository();
        var service = new WorkTaskService(repository);
        var overdue = service.Create("补发报价", dueDate: DateOnly.FromDateTime(DateTime.Today.AddDays(-1)));
        var completed = service.Create("归档报价", dueDate: DateOnly.FromDateTime(DateTime.Today.AddDays(-1)));
        completed.Complete();

        var result = service.List(WorkTaskFilter.Overdue);

        Assert.Contains(overdue, result);
        Assert.DoesNotContain(completed, result);
    }

    [Fact]
    public void Edit_UpdatesRepositoryAndTask()
    {
        var repository = new TestRepository();
        var service = new WorkTaskService(repository);
        var task = service.Create("待修改");

        service.Edit(task, "已修改", "新备注", DateOnly.FromDateTime(DateTime.Today.AddDays(5)));

        Assert.Equal("已修改", task.Title);
        Assert.Equal("新备注", task.Description);
        Assert.Equal(1, repository.UpdatedCount);
    }

    private sealed class TestRepository : IWorkTaskRepository
    {
        private readonly List<WorkTask> tasks = [];
        public int AddedCount { get; private set; }
        public int UpdatedCount { get; private set; }
        public IReadOnlyList<WorkTask> List() => tasks;
        public void Add(WorkTask task) { tasks.Add(task); AddedCount++; }
        public void Update(WorkTask task) => UpdatedCount++;
        public void Remove(Guid taskId) => tasks.RemoveAll(item => item.Id == taskId);
    }
}
