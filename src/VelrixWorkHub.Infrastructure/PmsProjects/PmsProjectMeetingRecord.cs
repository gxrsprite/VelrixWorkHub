using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmsProjects;

[Table(Name = "PmsProjectMeeting")]
public sealed class PmsProjectMeetingRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid ProjectId { get; set; }
    [Column(StringLength = 200, IsNullable = false, Position = 3)] public string Title { get; set; } = string.Empty;
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 4)] public PmsProjectMeetingType MeetingType { get; set; }
    [Column(IsNullable = false, Position = 5)] public DateTime StartsAt { get; set; }
    [Column(IsNullable = false, Position = 6)] public DateTime EndsAt { get; set; }
    [Column(StringLength = 500, IsNullable = true, Position = 7)] public string? LocationOrMode { get; set; }
    [Column(StringLength = 100, IsNullable = true, Position = 8)] public string? HostName { get; set; }
    [Column(StringLength = 2000, IsNullable = true, Position = 9)] public string? ParticipantNames { get; set; }
    [Column(StringLength = 8000, IsNullable = true, Position = 10)] public string? Minutes { get; set; }
    [Column(StringLength = 8000, IsNullable = true, Position = 11)] public string? Decisions { get; set; }
    [Column(StringLength = 4000, IsNullable = false, Position = 12)] public string OtherInfo { get; set; } = "{}";
    [Column(ServerTime = DateTimeKind.Local, Position = 13)] public DateTime CreatedTime { get; set; }
    [Column(ServerTime = DateTimeKind.Local, Position = 14)] public DateTime ModifiedTime { get; set; }
}
