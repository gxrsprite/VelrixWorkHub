using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmpProjects;

[Table(Name = "PmpProjectWorkItemActivity")]
public sealed class PmpProjectWorkItemActivityRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid WorkItemId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 3)] public PmpProjectWorkItemActivityType Type { get; set; }
    [Column(StringLength = 4000, IsNullable = true, Position = 4)] public string? Content { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 5)] public string ActorName { get; set; } = string.Empty;
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = true, Position = 6)] public PmpProjectWorkItemStatus? PreviousStatus { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = true, Position = 7)] public PmpProjectWorkItemStatus? CurrentStatus { get; set; }
    [Column(IsNullable = false, Position = 8)] public DateTime OccurredAt { get; set; }
}
