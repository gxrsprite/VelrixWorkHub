using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmpProjects;

public interface IPmpProjectMeetingRepository
{
    IReadOnlyList<PmpProjectMeeting> List(Guid? projectId = null);
    void Add(PmpProjectMeeting item);
    void Update(PmpProjectMeeting item);
    void Remove(Guid id);
}

public sealed class PmpProjectMeetingService(IPmpProjectMeetingRepository repository, IPmpProjectRepository projects, PmpProjectWorkItemService workItems)
{
    public IReadOnlyList<PmpProjectMeeting> List(Guid? projectId = null, string? keyword = null)
    {
        var text = keyword?.Trim();
        return repository.List(projectId)
            .Where(x => string.IsNullOrWhiteSpace(text) || x.Title.Contains(text, StringComparison.OrdinalIgnoreCase) || (x.HostName?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderByDescending(x => x.StartsAt)
            .ToArray();
    }

    public PmpProjectMeeting Create(Guid projectId, string title, PmpProjectMeetingType meetingType, DateTime startsAt, DateTime endsAt, string? locationOrMode, string? hostName, string? participantNames, string? minutes, string? decisions, string? otherInfo)
    {
        EnsureProject(projectId);
        var item = new PmpProjectMeeting(projectId, title, meetingType, startsAt, endsAt, locationOrMode, hostName, participantNames, minutes, decisions, otherInfo);
        repository.Add(item);
        return item;
    }

    public void Edit(PmpProjectMeeting item, string title, PmpProjectMeetingType meetingType, DateTime startsAt, DateTime endsAt, string? locationOrMode, string? hostName, string? participantNames, string? minutes, string? decisions, string? otherInfo)
    {
        var stored = EnsureStored(item.Id);
        EnsureProject(stored.ProjectId);
        stored.Edit(stored.ProjectId, title, meetingType, startsAt, endsAt, locationOrMode, hostName, participantNames, minutes, decisions, otherInfo);
        repository.Update(stored);
    }

    public PmpProjectWorkItem CreateActionItem(Guid meetingId, Guid? parentId, string title, string? description, string? ownerName, string? participantNames, PmpProjectWorkItemPriority priority, DateTime? plannedStartAt, DateTime? plannedEndAt, string? otherInfo)
    {
        var meeting = EnsureStored(meetingId);
        return workItems.Create(meeting.ProjectId, parentId, nameof(PmpProjectMeeting), meeting.Id, title, description, ownerName, participantNames, priority, plannedStartAt, plannedEndAt, otherInfo);
    }

    public IReadOnlyList<PmpProjectWorkItem> ListActionItems(Guid meetingId)
    {
        var meeting = EnsureStored(meetingId);
        return workItems.List(meeting.ProjectId).Where(x => x.SourceType == nameof(PmpProjectMeeting) && x.SourceId == meeting.Id).ToArray();
    }

    public void Remove(PmpProjectMeeting item)
    {
        if (ListActionItems(item.Id).Count > 0) throw new InvalidOperationException("会议已有行动项，不能删除。请保留会议记录以维持来源追溯。");
        repository.Remove(item.Id);
    }

    private PmpProjectMeeting EnsureStored(Guid id) => repository.List().FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("会议不存在或已被删除。");
    private PmpProject EnsureProject(Guid id) => projects.List().FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("关联项目不存在。");
}
