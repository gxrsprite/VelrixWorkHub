using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Attachments;

[Table(Name = "BusinessAttachment")]
public sealed class BusinessAttachmentRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 2)] public string BusinessType { get; set; } = string.Empty;
    [Column(Position = 3, IsNullable = false)] public Guid BusinessId { get; set; }
    [Column(StringLength = 260, IsNullable = false, Position = 4)] public string FileName { get; set; } = string.Empty;
    [Column(StringLength = 120, IsNullable = false, Position = 5)] public string ContentType { get; set; } = string.Empty;
    [Column(Position = 6, IsNullable = false)] public long SizeBytes { get; set; }
    [Column(StringLength = 64, IsNullable = false, Position = 7)] public string Sha256 { get; set; } = string.Empty;
    [Column(StringLength = 500, IsNullable = false, Position = 8)] public string StorageKey { get; set; } = string.Empty;
    [Column(Position = 9, IsNullable = false)] public int VersionNumber { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 10)] public string UploadedBy { get; set; } = string.Empty;
    [Column(Position = 11, IsNullable = false, ServerTime = DateTimeKind.Local)] public DateTime UploadedAt { get; set; }
    [Column(StringLength = -1, IsNullable = false, Position = 12)] public string OtherInfo { get; set; } = "{}";
    [Column(MapType = typeof(string), StringLength = 50, Position = 13, IsNullable = false)] public BusinessAttachmentStatus Status { get; set; }
    [Column(StringLength = 500, IsNullable = false, Position = 14)] public string DeletedReason { get; set; } = string.Empty;
    [Column(Position = 15, IsNullable = true, ServerTime = DateTimeKind.Local)] public DateTime? DeletedAt { get; set; }
}
