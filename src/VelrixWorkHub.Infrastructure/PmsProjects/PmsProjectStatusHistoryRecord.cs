using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmsProjects;

[Table(Name = "PmsProjectStatusHistory")]
public sealed class PmsProjectStatusHistoryRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid ProjectId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 3)] public PmsProjectStatus FromStatus { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 4)] public PmsProjectStatus ToStatus { get; set; }
    [Column(StringLength = 2000, IsNullable = false, Position = 5)] public string Reason { get; set; } = string.Empty;
    [Column(StringLength = 100, IsNullable = false, Position = 6)] public string ActorName { get; set; } = string.Empty;
    [Column(Position = 7, ServerTime = DateTimeKind.Local)] public DateTime ChangedAt { get; set; }
}
