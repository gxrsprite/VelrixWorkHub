using FreeSql.DataAnnotations;
using VelrixWorkHub.Application.Notifications;

namespace VelrixWorkHub.Infrastructure.Notifications;

[Table(Name = "OaNotificationFailure")]
public sealed class NotificationFailureRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 50, IsNullable = false, Position = 2)] public string Operation { get; set; } = string.Empty;
    [Column(StringLength = 100, IsNullable = false, Position = 3)] public string Recipient { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 4)] public string DedupeKey { get; set; } = string.Empty;
    [Column(StringLength = 4000, Position = 5)] public string? PayloadJson { get; set; }
    [Column(StringLength = 2000, IsNullable = false, Position = 6)] public string Error { get; set; } = string.Empty;
    [Column(IsNullable = false, Position = 7)] public DateTime OccurredAt { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 8)] public NotificationFailureStatus Status { get; set; } = NotificationFailureStatus.Pending;
    [Column(IsNullable = false, Position = 9)] public int RetryCount { get; set; }
    [Column(Position = 10)] public DateTime? LastRetryAt { get; set; }
    [Column(Position = 11)] public DateTime? ResolvedAt { get; set; }
}
