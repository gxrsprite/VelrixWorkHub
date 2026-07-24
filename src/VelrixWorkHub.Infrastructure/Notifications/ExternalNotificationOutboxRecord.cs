using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Notifications;

[Table(Name = "OaExternalNotificationOutbox")]
[Index("OaExternalNotificationOutbox_uk_Channel_Address_DedupeKey", "Channel,Address,DedupeKey", true)]
[Index("OaExternalNotificationOutbox_ix_Status_NextAttemptAt", "Status,NextAttemptAt", false)]
public sealed class ExternalNotificationOutboxRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid NotificationId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 3)] public ExternalNotificationChannel Channel { get; set; }
    [Column(StringLength = 500, IsNullable = false, Position = 4)] public string Address { get; set; } = string.Empty;
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 5)] public WorkNotificationKind Kind { get; set; }
    [Column(StringLength = 200, IsNullable = false, Position = 6)] public string Title { get; set; } = string.Empty;
    [Column(StringLength = 2000, IsNullable = false, Position = 7)] public string Content { get; set; } = string.Empty;
    [Column(StringLength = 500, Position = 8)] public string? Href { get; set; }
    [Column(StringLength = 500, IsNullable = false, Position = 9)] public string DedupeKey { get; set; } = string.Empty;
    [Column(IsNullable = false, Position = 10)] public DateTime CreatedAt { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 11)] public ExternalNotificationDeliveryStatus Status { get; set; } = ExternalNotificationDeliveryStatus.Pending;
    [Column(IsNullable = false, Position = 12)] public int RetryCount { get; set; }
    [Column(Position = 13)] public DateTime? LastAttemptAt { get; set; }
    [Column(Position = 14)] public DateTime? DeliveredAt { get; set; }
    [Column(StringLength = 2000, Position = 15)] public string? LastError { get; set; }
    [Column(Position = 16)] public DateTime? NextAttemptAt { get; set; }
}
