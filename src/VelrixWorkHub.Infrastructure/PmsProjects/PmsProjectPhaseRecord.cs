using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmsProjects;

[Table(Name = "PmsProjectPhase")]
public sealed class PmsProjectPhaseRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid ProjectId { get; set; }
    [Column(StringLength = 200, IsNullable = false, Position = 3)] public string Name { get; set; } = string.Empty;
    [Column(MapType = typeof(string), StringLength = 50, Position = 4)] public PmsProjectPhaseKind Kind { get; set; }
    [Column(Position = 5)] public int Sequence { get; set; }
    [Column(Position = 6, DbType = "date", IsNullable = false)] public DateTime PlannedStart { get; set; }
    [Column(Position = 7, DbType = "date", IsNullable = false)] public DateTime PlannedEnd { get; set; }
    [Column(Position = 8)] public int PercentComplete { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 9)] public PmsProjectPhaseStatus Status { get; set; }
    [Column(Position = 10, ServerTime = DateTimeKind.Local)] public DateTime CreatedTime { get; set; }
    [Column(Position = 11, ServerTime = DateTimeKind.Local)] public DateTime ModifiedTime { get; set; }
}
