using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PmsProjectMeetingServiceTests
{
    [Fact]
    public void MeetingService_CreatesProjectBoundActionItems()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmsProject("PRJ-MEETING", "会议项目", null, null, today, today.AddDays(30));
        var otherProject = new PmsProject("PRJ-MEETING-OTHER", "其他项目", null, null, today, today.AddDays(30));
        var projects = new ProjectRepository(project, otherProject);
        var workItems = new WorkItemRepository();
        var workItemService = new PmsProjectWorkItemService(workItems, projects);
        var meetings = new MeetingRepository();
        var service = new PmsProjectMeetingService(meetings, projects, workItemService);
        var meeting = service.Create(project.Id, "项目周会", PmsProjectMeetingType.Internal, DateTime.Today.AddHours(9), DateTime.Today.AddHours(10), "会议室 A", "项目经理", "开发负责人", "确认本周范围", "周五前完成接口联调", "{}");

        var action = service.CreateActionItem(meeting.Id, null, "完成接口联调", "同步接口结果", "开发负责人", "测试负责人", PmsProjectWorkItemPriority.High, DateTime.Today, DateTime.Today.AddDays(3), "{}");

        Assert.Equal(project.Id, action.ProjectId);
        Assert.Equal(nameof(PmsProjectMeeting), action.SourceType);
        Assert.Equal(meeting.Id, action.SourceId);
        Assert.Single(service.ListActionItems(meeting.Id));
        var otherProjectItem = workItemService.Create(otherProject.Id, null, null, null, "其他项目行动项", null, null, null, PmsProjectWorkItemPriority.Medium, null, null, "{}");
        Assert.Throws<InvalidOperationException>(() => service.CreateActionItem(meeting.Id, otherProjectItem.Id, "伪造跨项目行动项", null, null, null, PmsProjectWorkItemPriority.Medium, null, null, "{}"));
        Assert.Throws<InvalidOperationException>(() => service.Remove(meeting));
    }

    [Fact]
    public void MeetingService_RejectsInvalidTimesAndMissingMeetingForAction()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmsProject("PRJ-MEETING-2", "会议项目", null, null, today, today.AddDays(30));
        var projects = new ProjectRepository(project);
        var service = new PmsProjectMeetingService(new MeetingRepository(), projects, new PmsProjectWorkItemService(new WorkItemRepository(), projects));

        Assert.Throws<ArgumentException>(() => service.Create(project.Id, "时间错误", PmsProjectMeetingType.Internal, DateTime.Today.AddHours(10), DateTime.Today.AddHours(9), null, null, null, null, null, "{}"));
        Assert.Throws<InvalidOperationException>(() => service.CreateActionItem(Guid.CreateVersion7(), null, "伪造行动项", null, null, null, PmsProjectWorkItemPriority.Medium, null, null, "{}"));
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

    private sealed class MeetingRepository : IPmsProjectMeetingRepository
    {
        private readonly List<PmsProjectMeeting> data = [];
        public IReadOnlyList<PmsProjectMeeting> List(Guid? projectId = null) => projectId is Guid id ? data.Where(x => x.ProjectId == id).ToArray() : data;
        public void Add(PmsProjectMeeting item) => data.Add(item);
        public void Update(PmsProjectMeeting item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }
}
