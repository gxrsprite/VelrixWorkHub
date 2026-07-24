using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Workflow;

[Table(Name = "WorkflowTask")]
public sealed class WorkflowTaskRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid InstanceId { get; set; }
    [Column(IsNullable = false, Position = 3)] public Guid DefinitionId { get; set; }
    [Column(IsNullable = false, Position = 4)] public int DefinitionVersion { get; set; }
    [Column(IsNullable = false, Position = 5)] public Guid NodeId { get; set; }
    [Column(StringLength = 200, IsNullable = false, Position = 6)] public string NodeName { get; set; } = string.Empty;
    [Column(StringLength = 100, IsNullable = false, Position = 7)] public string BusinessType { get; set; } = string.Empty;
    [Column(IsNullable = false, Position = 8)] public Guid BusinessId { get; set; }
    [Column(StringLength = 200, IsNullable = false, Position = 9)] public string Assignee { get; set; } = string.Empty;
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 10)] public WorkflowTaskStatus Status { get; set; }
    [Column(StringLength = 200, Position = 11)] public string? TransferTarget { get; set; }
    [Column(StringLength = 2000, Position = 12)] public string? DecisionComment { get; set; }
    [Column(StringLength = 200, Position = 13)] public string? DecisionActor { get; set; }
    [Column(IsNullable = false, Position = 14)] public DateTime CreatedAt { get; set; }
    [Column(Position = 15)] public DateTime? CompletedAt { get; set; }
    [Column(IsNullable = false, Position = 16)] public long Revision { get; set; }
    [Column(IsNullable = false, Position = 17)] public int Round { get; set; } = 1;
}
