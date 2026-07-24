using FreeSql.DataAnnotations;

namespace VelrixWorkHub.Infrastructure.PmpProjects;

[Table(Name = "PmpProjectBaseline")]
public sealed class PmpProjectBaselineRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid ProjectId { get; set; }
    [Column(Position = 3, IsNullable = false)] public int VersionNumber { get; set; }
    [Column(StringLength = 200, IsNullable = false, Position = 4)] public string Label { get; set; } = string.Empty;
    [Column(Position = 5, IsNullable = false, ServerTime = DateTimeKind.Local)] public DateTime SnapshotTime { get; set; }
    [Column(Position = 6, DbType = "date", IsNullable = false)] public DateTime PlannedStart { get; set; }
    [Column(Position = 7, DbType = "date", IsNullable = false)] public DateTime PlannedEnd { get; set; }
    [Column(Position = 8, IsNullable = false)] public int PercentComplete { get; set; }
    [Column(Position = 9, IsNullable = false)] public int PhaseCount { get; set; }
    [Column(Position = 10, IsNullable = false)] public int TaskCount { get; set; }
}
