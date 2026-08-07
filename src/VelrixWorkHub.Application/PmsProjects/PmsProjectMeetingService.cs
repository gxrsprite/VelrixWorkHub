using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmsProjects;

public interface IPmsProjectMeetingRepository
{
    IReadOnlyList<PmsProjectMeeting> List(Guid? projectId = null);
    void Add(PmsProjectMeeting item);
    void Update(PmsProjectMeeting item);
    void Remove(Guid id);
}

public sealed class PmsProjectMeetingService(IPmsProjectMeetingRepository repository, IPmsProjectRepository projects, PmsProjectWorkItemService workItems)
{
    public IReadOnlyList<PmsProjectMeeting> List(Guid? projectId = null, string? keyword = null)
    {
        var text = keyword?.Trim();
        return repository.List(projectId)
            .Where(x => string.IsNullOrWhiteSpace(text) || x.Title.Contains(text, StringComparison.OrdinalIgnoreCase) || (x.HostName?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderByDescending(x => x.StartsAt)
            .ToArray();
    }

    public PmsProjectMeeting Create(Guid projectId, string title, PmsProjectMeetingType meetingType, DateTime startsAt, DateTime endsAt, string? locationOrMode, string? hostName, string? participantNames, string? minutes, string? decisions, string? otherInfo)
    {
        EnsureProject(projectId);
        var item = new PmsProjectMeeting(projectId, title, meetingType, startsAt, endsAt, locationOrMode, hostName, participantNames, minutes, decisions, otherInfo);
        repository.Add(item);
        return item;
    }

    public void Edit(PmsProjectMeeting item, string title, PmsProjectMeetingType meetingType, DateTime startsAt, DateTime endsAt, string? locationOrMode, string? hostName, string? participantNames, string? minutes, string? decisions, string? otherInfo)
    {
        var stored = EnsureStored(item.Id);
        EnsureProject(stored.ProjectId);
        stored.Edit(stored.ProjectId, title, meetingType, startsAt, endsAt, locationOrMode, hostName, participantNames, minutes, decisions, otherInfo);
        repository.Update(stored);
    }

    public PmsProjectWorkItem CreateActionItem(Guid meetingId, Guid? parentId, string title, string? description, string? ownerName, string? participantNames, PmsProjectWorkItemPriority priority, DateTime? plannedStartAt, DateTime? plannedEndAt, string? otherInfo)
    {
        var meeting = EnsureStored(meetingId);
        return workItems.Create(meeting.ProjectId, parentId, nameof(PmsProjectMeeting), meeting.Id, title, description, ownerName, participantNames, priority, plannedStartAt, plannedEndAt, otherInfo);
    }

    public IReadOnlyList<PmsProjectWorkItem> ListActionItems(Guid meetingId)
    {
        var meeting = EnsureStored(meetingId);
        return workItems.List(meeting.ProjectId).Where(x => x.SourceType == nameof(PmsProjectMeeting) && x.SourceId == meeting.Id).ToArray();
    }

    public void Remove(PmsProjectMeeting item)
    {
        if (ListActionItems(item.Id).Count > 0) throw new InvalidOperationException("会议已有行动项，不能删除。请保留会议记录以维持来源追溯。");
        repository.Remove(item.Id);
    }

    private PmsProjectMeeting EnsureStored(Guid id) => repository.List().FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("会议不存在或已被删除。");
    private PmsProject EnsureProject(Guid id) => projects.List().FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("关联项目不存在。");
}
