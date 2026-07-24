using FreeSql.DataAnnotations;

namespace VelrixWorkHub.Infrastructure.Notifications;

[Table(Name = "NotificationFailureAudit")]
public sealed class NotificationFailureAuditRecord
{
    [Column(IsPrimary = true, StringLength = 50)] public Guid Id { get; set; }
    [Column(StringLength = 50, IsNullable = false)] public Guid FailureId { get; set; }
    [Column(StringLength = 50, IsNullable = false)] public string Action { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false)] public string Actor { get; set; } = string.Empty;
    [Column(StringLength = 2000, IsNullable = false)] public string Details { get; set; } = string.Empty;
    [Column(IsNullable = false)] public DateTime OccurredAt { get; set; }
}
