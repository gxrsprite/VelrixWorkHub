using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmsProjects;

[Table(Name = "PmsProjectWorkItem")]
public sealed class PmsProjectWorkItemRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid ProjectId { get; set; }
    [Column(IsNullable = true, Position = 3)] public Guid? ParentId { get; set; }
    [Column(StringLength = 100, IsNullable = true, Position = 4)] public string? SourceType { get; set; }
    [Column(IsNullable = true, Position = 5)] public Guid? SourceId { get; set; }
    [Column(StringLength = 200, IsNullable = false, Position = 6)] public string Title { get; set; } = string.Empty;
    [Column(StringLength = 4000, IsNullable = true, Position = 7)] public string? Description { get; set; }
    [Column(IsNullable = true, Position = 8)] public Guid? OwnerUserId { get; set; }
    [Column(StringLength = 100, IsNullable = true, Position = 9)] public string? OwnerName { get; set; }
    [Column(StringLength = 2000, IsNullable = true, Position = 10)] public string? ParticipantUserIdsJson { get; set; }
    [Column(StringLength = 2000, IsNullable = true, Position = 11)] public string? ParticipantNames { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 12)] public PmsProjectWorkItemPriority Priority { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 13)] public PmsProjectWorkItemStatus Status { get; set; }
    [Column(IsNullable = true, Position = 14)] public DateTime? PlannedStartAt { get; set; }
    [Column(IsNullable = true, Position = 15)] public DateTime? PlannedEndAt { get; set; }
    [Column(IsNullable = true, Position = 16)] public DateTime? ReminderAt { get; set; }
    [Column(IsNullable = true, Position = 17)] public DateTime? ActualStartAt { get; set; }
    [Column(IsNullable = true, Position = 18)] public DateTime? ActualEndAt { get; set; }
    [Column(StringLength = 4000, IsNullable = true, Position = 19)] public string? Feedback { get; set; }
    [Column(StringLength = 1000, IsNullable = true, Position = 20)] public string? CompletionRejectionReason { get; set; }
    [Column(StringLength = 1000, IsNullable = false, Position = 21)] public string OtherInfo { get; set; } = "{}";
    [Column(ServerTime = DateTimeKind.Local, Position = 22)] public DateTime CreatedTime { get; set; }
    [Column(ServerTime = DateTimeKind.Local, Position = 23)] public DateTime ModifiedTime { get; set; }
    [Column(StringLength = 2000, IsNullable = true, Position = 24)] public string? VisibilityOrganizationIdsJson { get; set; }
    [Column(StringLength = 2000, IsNullable = true, Position = 25)] public string? VisibilityRoleIdsJson { get; set; }
}
