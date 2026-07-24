using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Announcements;
[Table(Name = "OaAnnouncement")]
public sealed class AnnouncementRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 200, IsNullable = false, Position = 2)] public string Title { get; set; } = string.Empty;
    [Column(StringLength = 10000, IsNullable = false, Position = 3)] public string Content { get; set; } = string.Empty;
    [Column(MapType = typeof(string), StringLength = 50, Position = 4)] public AnnouncementStatus Status { get; set; }
    [Column(Position = 5)] public DateTime? PublishedTime { get; set; }
    [Column(Position = 6, ServerTime = DateTimeKind.Local)] public DateTime CreatedTime { get; set; }
    [Column(Position = 7, ServerTime = DateTimeKind.Local)] public DateTime ModifiedTime { get; set; }
}
