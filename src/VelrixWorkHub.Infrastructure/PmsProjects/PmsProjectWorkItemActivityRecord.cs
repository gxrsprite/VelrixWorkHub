using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmsProjects;

[Table(Name = "PmsProjectWorkItemActivity")]
public sealed class PmsProjectWorkItemActivityRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid WorkItemId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 3)] public PmsProjectWorkItemActivityType Type { get; set; }
    [Column(StringLength = 4000, IsNullable = true, Position = 4)] public string? Content { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 5)] public string ActorName { get; set; } = string.Empty;
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = true, Position = 6)] public PmsProjectWorkItemStatus? PreviousStatus { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = true, Position = 7)] public PmsProjectWorkItemStatus? CurrentStatus { get; set; }
    [Column(IsNullable = false, Position = 8)] public DateTime OccurredAt { get; set; }
}
