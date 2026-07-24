namespace VelrixWorkHub.Domain;

public enum PmpProjectMeetingType { Internal, Customer, Steering, Review, Other }

public sealed class PmpProjectMeeting
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ProjectId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public PmpProjectMeetingType MeetingType { get; private set; }
    public DateTime StartsAt { get; private set; }
    public DateTime EndsAt { get; private set; }
    public string? LocationOrMode { get; private set; }
    public string? HostName { get; private set; }
    public string? ParticipantNames { get; private set; }
    public string? Minutes { get; private set; }
    public string? Decisions { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public PmpProjectMeeting(Guid projectId, string title, PmpProjectMeetingType meetingType, DateTime startsAt, DateTime endsAt, string? locationOrMode, string? hostName, string? participantNames, string? minutes, string? decisions, string? otherInfo)
        => Edit(projectId, title, meetingType, startsAt, endsAt, locationOrMode, hostName, participantNames, minutes, decisions, otherInfo);

    public static PmpProjectMeeting Restore(Guid id, Guid projectId, string title, PmpProjectMeetingType meetingType, DateTime startsAt, DateTime endsAt, string? locationOrMode, string? hostName, string? participantNames, string? minutes, string? decisions, string? otherInfo)
        => new(projectId, title, meetingType, startsAt, endsAt, locationOrMode, hostName, participantNames, minutes, decisions, otherInfo) { Id = id };

    public void Edit(Guid projectId, string title, PmpProjectMeetingType meetingType, DateTime startsAt, DateTime endsAt, string? locationOrMode, string? hostName, string? participantNames, string? minutes, string? decisions, string? otherInfo)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("必须关联项目。", nameof(projectId));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("会议主题不能为空。", nameof(title));
        if (endsAt <= startsAt) throw new ArgumentException("会议结束时间必须晚于开始时间。", nameof(endsAt));
        ProjectId = projectId;
        Title = title.Trim();
        MeetingType = meetingType;
        StartsAt = startsAt;
        EndsAt = endsAt;
        LocationOrMode = Clean(locationOrMode);
        HostName = Clean(hostName);
        ParticipantNames = Clean(participantNames);
        Minutes = Clean(minutes);
        Decisions = Clean(decisions);
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
