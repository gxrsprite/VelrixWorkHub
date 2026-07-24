using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmpProjects;

[Table(Name = "PmpWbsTask")]
public sealed class PmpWbsTaskRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid ProjectId { get; set; }
    [Column(Position = 3)] public Guid? ParentId { get; set; }
    [Column(StringLength = 200, IsNullable = false, Position = 4)] public string Title { get; set; } = string.Empty;
    [Column(StringLength = 100, Position = 5)] public string? AssigneeName { get; set; }
    [Column(Position = 6)] public int Sequence { get; set; }
    [Column(Position = 7, DbType = "date", IsNullable = false)] public DateTime PlannedStart { get; set; }
    [Column(Position = 8, DbType = "date", IsNullable = false)] public DateTime PlannedEnd { get; set; }
    [Column(Position = 9)] public int PercentComplete { get; set; }
    [Column(Position = 10)] public bool IsMilestone { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 11)] public PmpWbsTaskStatus Status { get; set; }
    [Column(Position = 12, ServerTime = DateTimeKind.Local)] public DateTime CreatedTime { get; set; }
    [Column(Position = 13, ServerTime = DateTimeKind.Local)] public DateTime ModifiedTime { get; set; }
}
