using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Workflow;

[Table(Name = "WorkflowOperation")]
[Index("WorkflowOperation_uk_DedupeKey", "DedupeKey", true)]
public sealed class WorkflowOperationRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid InstanceId { get; set; }
    [Column(Position = 3)] public Guid? TaskId { get; set; }
    [Column(Position = 4)] public Guid? NodeId { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 5)] public string BusinessType { get; set; } = string.Empty;
    [Column(IsNullable = false, Position = 6)] public Guid BusinessId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 7)] public WorkflowOperationKind Kind { get; set; }
    [Column(StringLength = 200, IsNullable = false, Position = 8)] public string Actor { get; set; } = string.Empty;
    [Column(StringLength = 200, Position = 9)] public string? TargetAssignee { get; set; }
    [Column(StringLength = 2000, Position = 10)] public string? Comment { get; set; }
    [Column(StringLength = 200, IsNullable = false, Position = 11)] public string DedupeKey { get; set; } = string.Empty;
    [Column(IsNullable = false, Position = 12, ServerTime = DateTimeKind.Local)] public DateTime OccurredAt { get; set; }
}
