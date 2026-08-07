using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PmsProjectWorkItemServiceTests
{
    [Fact]
    public void WorkItemService_EnforcesParentDatesAndLifecycle()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmsProject("PRJ-WORK", "工作项项目", null, null, today, today.AddDays(30));
        var repository = new WorkItemRepository();
        var service = new PmsProjectWorkItemService(repository, new ProjectRepository(project));
        var parent = service.Create(project.Id, null, "Meeting", Guid.CreateVersion7(), "会议行动项", null, "负责人", "参与人 A", PmsProjectWorkItemPriority.High, DateTime.Today, DateTime.Today.AddDays(2), "{\"source\":\"会议\"}");
        var child = service.Create(project.Id, parent.Id, null, null, "子工作项", null, null, null, PmsProjectWorkItemPriority.Medium, null, null, "{}");

        Assert.Throws<InvalidOperationException>(() => service.Remove(parent));
        Assert.Throws<ArgumentException>(() => service.Create(project.Id, null, null, null, "日期错误", null, null, null, PmsProjectWorkItemPriority.Low, DateTime.Today, DateTime.Today.AddDays(-1), "{}"));
        service.SetStatus(child, PmsProjectWorkItemStatus.Open, null);
        service.SetStatus(child, PmsProjectWorkItemStatus.InProgress, null);
        Assert.Throws<InvalidOperationException>(() => service.SetStatus(child, PmsProjectWorkItemStatus.Completed, "已交付"));
        service.SetStatus(child, PmsProjectWorkItemStatus.PendingApproval, "已交付");
        service.ApplyCompletionApproval(child);
        Assert.Equal(PmsProjectWorkItemStatus.Completed, child.Status);
        Assert.NotNull(child.ActualStartAt);
        Assert.NotNull(child.ActualEndAt);
        Assert.Throws<InvalidOperationException>(() => service.Edit(child, null, null, null, "不能编辑", null, null, null, PmsProjectWorkItemPriority.Low, null, null, "{}"));
    }

    private sealed class ProjectRepository(params PmsProject[] items) : IPmsProjectRepository
    {
        private readonly List<PmsProject> data = [.. items];
        public IReadOnlyList<PmsProject> List() => data;
        public void Add(PmsProject item) => data.Add(item);
        public void Update(PmsProject item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }

    private sealed class WorkItemRepository : IPmsProjectWorkItemRepository
    {
        private readonly List<PmsProjectWorkItem> data = [];
        public IReadOnlyList<PmsProjectWorkItem> List(Guid? projectId = null) => projectId is Guid id ? data.Where(x => x.ProjectId == id).ToArray() : data;
        public void Add(PmsProjectWorkItem item) => data.Add(item);
        public void Update(PmsProjectWorkItem item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }
}
