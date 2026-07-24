using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Attachments;

[Table(Name = "AttachmentAudit")]
public sealed class AttachmentAuditRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid AttachmentId { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 3)] public string BusinessType { get; set; } = string.Empty;
    [Column(Position = 4, IsNullable = false)] public Guid BusinessId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 5, IsNullable = false)] public AttachmentAuditAction Action { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 6)] public string Actor { get; set; } = string.Empty;
    [Column(Position = 7, IsNullable = false, ServerTime = DateTimeKind.Local)] public DateTime OccurredAt { get; set; }
    [Column(StringLength = 500, IsNullable = false, Position = 8)] public string Details { get; set; } = string.Empty;
}
