using VelrixWorkHub.Application.PmpProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PmpProjectMeetingServiceTests
{
    [Fact]
    public void MeetingService_CreatesProjectBoundActionItems()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmpProject("PRJ-MEETING", "会议项目", null, null, today, today.AddDays(30));
        var otherProject = new PmpProject("PRJ-MEETING-OTHER", "其他项目", null, null, today, today.AddDays(30));
        var projects = new ProjectRepository(project, otherProject);
        var workItems = new WorkItemRepository();
        var workItemService = new PmpProjectWorkItemService(workItems, projects);
        var meetings = new MeetingRepository();
        var service = new PmpProjectMeetingService(meetings, projects, workItemService);
        var meeting = service.Create(project.Id, "项目周会", PmpProjectMeetingType.Internal, DateTime.Today.AddHours(9), DateTime.Today.AddHours(10), "会议室 A", "项目经理", "开发负责人", "确认本周范围", "周五前完成接口联调", "{}");

        var action = service.CreateActionItem(meeting.Id, null, "完成接口联调", "同步接口结果", "开发负责人", "测试负责人", PmpProjectWorkItemPriority.High, DateTime.Today, DateTime.Today.AddDays(3), "{}");

        Assert.Equal(project.Id, action.ProjectId);
        Assert.Equal(nameof(PmpProjectMeeting), action.SourceType);
        Assert.Equal(meeting.Id, action.SourceId);
        Assert.Single(service.ListActionItems(meeting.Id));
        var otherProjectItem = workItemService.Create(otherProject.Id, null, null, null, "其他项目行动项", null, null, null, PmpProjectWorkItemPriority.Medium, null, null, "{}");
        Assert.Throws<InvalidOperationException>(() => service.CreateActionItem(meeting.Id, otherProjectItem.Id, "伪造跨项目行动项", null, null, null, PmpProjectWorkItemPriority.Medium, null, null, "{}"));
        Assert.Throws<InvalidOperationException>(() => service.Remove(meeting));
    }

    [Fact]
    public void MeetingService_RejectsInvalidTimesAndMissingMeetingForAction()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmpProject("PRJ-MEETING-2", "会议项目", null, null, today, today.AddDays(30));
        var projects = new ProjectRepository(project);
        var service = new PmpProjectMeetingService(new MeetingRepository(), projects, new PmpProjectWorkItemService(new WorkItemRepository(), projects));

        Assert.Throws<ArgumentException>(() => service.Create(project.Id, "时间错误", PmpProjectMeetingType.Internal, DateTime.Today.AddHours(10), DateTime.Today.AddHours(9), null, null, null, null, null, "{}"));
        Assert.Throws<InvalidOperationException>(() => service.CreateActionItem(Guid.CreateVersion7(), null, "伪造行动项", null, null, null, PmpProjectWorkItemPriority.Medium, null, null, "{}"));
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

    private sealed class MeetingRepository : IPmpProjectMeetingRepository
    {
        private readonly List<PmpProjectMeeting> data = [];
        public IReadOnlyList<PmpProjectMeeting> List(Guid? projectId = null) => projectId is Guid id ? data.Where(x => x.ProjectId == id).ToArray() : data;
        public void Add(PmpProjectMeeting item) => data.Add(item);
        public void Update(PmpProjectMeeting item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }
}
