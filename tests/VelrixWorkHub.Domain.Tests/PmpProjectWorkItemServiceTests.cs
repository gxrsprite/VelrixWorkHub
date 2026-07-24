using VelrixWorkHub.Application.PmpProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PmpProjectWorkItemServiceTests
{
    [Fact]
    public void WorkItemService_EnforcesParentDatesAndLifecycle()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmpProject("PRJ-WORK", "工作项项目", null, null, today, today.AddDays(30));
        var repository = new WorkItemRepository();
        var service = new PmpProjectWorkItemService(repository, new ProjectRepository(project));
        var parent = service.Create(project.Id, null, "Meeting", Guid.CreateVersion7(), "会议行动项", null, "负责人", "参与人 A", PmpProjectWorkItemPriority.High, DateTime.Today, DateTime.Today.AddDays(2), "{\"source\":\"会议\"}");
        var child = service.Create(project.Id, parent.Id, null, null, "子工作项", null, null, null, PmpProjectWorkItemPriority.Medium, null, null, "{}");

        Assert.Throws<InvalidOperationException>(() => service.Remove(parent));
        Assert.Throws<ArgumentException>(() => service.Create(project.Id, null, null, null, "日期错误", null, null, null, PmpProjectWorkItemPriority.Low, DateTime.Today, DateTime.Today.AddDays(-1), "{}"));
        service.SetStatus(child, PmpProjectWorkItemStatus.Open, null);
        service.SetStatus(child, PmpProjectWorkItemStatus.InProgress, null);
        Assert.Throws<InvalidOperationException>(() => service.SetStatus(child, PmpProjectWorkItemStatus.Completed, "已交付"));
        service.SetStatus(child, PmpProjectWorkItemStatus.PendingApproval, "已交付");
        service.ApplyCompletionApproval(child);
        Assert.Equal(PmpProjectWorkItemStatus.Completed, child.Status);
        Assert.NotNull(child.ActualStartAt);
        Assert.NotNull(child.ActualEndAt);
        Assert.Throws<InvalidOperationException>(() => service.Edit(child, null, null, null, "不能编辑", null, null, null, PmpProjectWorkItemPriority.Low, null, null, "{}"));
    }

    private sealed class ProjectRepository(params PmpProject[] items) : IPmpProjectRepository
    {
        private readonly List<PmpProject> data = [.. items];
        public IReadOnlyList<PmpProject> List() => data;
        public void Add(PmpProject item) => data.Add(item);
        public void Update(PmpProject item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }

    private sealed class WorkItemRepository : IPmpProjectWorkItemRepository
    {
        private readonly List<PmpProjectWorkItem> data = [];
        public IReadOnlyList<PmpProjectWorkItem> List(Guid? projectId = null) => projectId is Guid id ? data.Where(x => x.ProjectId == id).ToArray() : data;
        public void Add(PmpProjectWorkItem item) => data.Add(item);
        public void Update(PmpProjectWorkItem item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }
}
