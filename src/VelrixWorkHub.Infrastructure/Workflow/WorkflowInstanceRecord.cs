using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Workflow;

[Table(Name = "WorkflowInstance")]
public sealed class WorkflowInstanceRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid DefinitionId { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 3)] public string DefinitionCode { get; set; } = string.Empty;
    [Column(IsNullable = false, Position = 4)] public int DefinitionVersion { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 5)] public string BusinessType { get; set; } = string.Empty;
    [Column(IsNullable = false, Position = 6)] public Guid BusinessId { get; set; }
    [Column(StringLength = 200, IsNullable = false, Position = 7)] public string StartedBy { get; set; } = "system";
    [Column(DbType = "text", IsNullable = false, Position = 8)] public string DefinitionSnapshotJson { get; set; } = "{}";
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 9)] public WorkflowInstanceStatus Status { get; set; }
    [Column(IsNullable = false, Position = 10)] public Guid CurrentNodeId { get; set; }
    [Column(Position = 11)] public DateTime StartedAt { get; set; }
    [Column(Position = 12)] public DateTime? CompletedAt { get; set; }
    [Column(Position = 13)] public Guid? PreviousInstanceId { get; set; }
    [Column(IsNullable = false, Position = 14)] public long Revision { get; set; }
    [Column(DbType = "text", IsNullable = false, Position = 15)] public string ActiveNodeIdsJson { get; set; } = "[]";
    [Column(DbType = "text", IsNullable = false, Position = 16)] public string ParallelJoinArrivalsJson { get; set; } = "{}";
    [Column(DbType = "text", IsNullable = false, Position = 17)] public string LoopIterationsJson { get; set; } = "{}";
    [Column(DbType = "text", IsNullable = false, Position = 18)] public string ApprovalAssigneesJson { get; set; } = "{}";
}
