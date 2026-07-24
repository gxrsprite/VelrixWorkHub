using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Notifications;

[Table(Name = "OaNotification")]
[Index("OaNotification_uk_Recipient_DedupeKey", "Recipient,DedupeKey", true)]
public sealed class NotificationRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 2)] public string Recipient { get; set; } = string.Empty;
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 3)] public WorkNotificationKind Kind { get; set; }
    [Column(StringLength = 200, IsNullable = false, Position = 4)] public string Title { get; set; } = string.Empty;
    [Column(StringLength = 2000, IsNullable = false, Position = 5)] public string Content { get; set; } = string.Empty;
    [Column(StringLength = 500, Position = 6)] public string? Href { get; set; }
    [Column(StringLength = 200, IsNullable = false, Position = 7)] public string DedupeKey { get; set; } = string.Empty;
    [Column(IsNullable = false, Position = 8, ServerTime = DateTimeKind.Local)] public DateTime CreatedAt { get; set; }
    [Column(Position = 9)] public DateTime? ReadAt { get; set; }
}
