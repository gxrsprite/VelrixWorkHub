using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmsProjects;

[Table(Name = "PmsDeliveryRecord")]
public sealed class PmsDeliveryRecordRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid ProjectId { get; set; }
    [Column(IsNullable = true, Position = 3)] public Guid? RequirementId { get; set; }
    [Column(IsNullable = true, Position = 4)] public Guid? WbsTaskId { get; set; }
    [Column(StringLength = 80, IsNullable = false, Position = 5)] public string RecordNo { get; set; } = string.Empty;
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 6)] public PmsDeliveryRecordType Type { get; set; }
    [Column(StringLength = 200, IsNullable = false, Position = 7)] public string Title { get; set; } = string.Empty;
    [Column(StringLength = 4000, IsNullable = true, Position = 8)] public string? Description { get; set; }
    [Column(StringLength = 100, IsNullable = true, Position = 9)] public string? OwnerName { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 10)] public PmsDeliveryRecordStatus Status { get; set; }
    [Column(StringLength = 4000, IsNullable = true, Position = 11)] public string? ReviewConclusion { get; set; }
    [Column(StringLength = 100, IsNullable = true, Position = 12)] public string? ReleaseVersion { get; set; }
    [Column(StringLength = 4000, IsNullable = true, Position = 13)] public string? ReleaseResult { get; set; }
    [Column(StringLength = 4000, IsNullable = false, Position = 14)] public string OtherInfo { get; set; } = "{}";
    [Column(ServerTime = DateTimeKind.Local, Position = 15)] public DateTime CreatedTime { get; set; }
    [Column(ServerTime = DateTimeKind.Local, Position = 16)] public DateTime ModifiedTime { get; set; }
}

[Table(Name = "PmsDeliveryRecordStatusHistory")]
public sealed class PmsDeliveryRecordStatusHistoryRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid DeliveryRecordId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 3)] public PmsDeliveryRecordStatus Status { get; set; }
    [Column(StringLength = 2000, IsNullable = true, Position = 4)] public string? Note { get; set; }
    [Column(StringLength = 100, IsNullable = true, Position = 5)] public string? ActorName { get; set; }
    [Column(IsNullable = false, Position = 6)] public DateTime OccurredAt { get; set; }
}
