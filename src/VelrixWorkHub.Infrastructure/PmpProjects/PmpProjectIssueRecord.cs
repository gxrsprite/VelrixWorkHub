using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.PmpProjects;
[Table(Name = "PmpProjectIssue")]
public sealed class PmpProjectIssueRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid ProjectId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 3)] public PmpProjectIssueKind Kind { get; set; }
    [Column(StringLength = 200, IsNullable = false, Position = 4)] public string Title { get; set; } = string.Empty;
    [Column(StringLength = 4000, Position = 5)] public string? Description { get; set; }
    [Column(StringLength = 100, Position = 6)] public string? OwnerName { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 7)] public PmpProjectIssuePriority Priority { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 8)] public PmpProjectIssueStatus Status { get; set; }
    [Column(Position = 9, DbType = "date")] public DateTime? DueDate { get; set; }
    [Column(Position = 10, ServerTime = DateTimeKind.Local)] public DateTime CreatedTime { get; set; }
    [Column(Position = 11, ServerTime = DateTimeKind.Local)] public DateTime ModifiedTime { get; set; }
}
