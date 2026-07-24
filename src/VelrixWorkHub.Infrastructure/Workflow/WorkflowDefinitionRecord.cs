using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Workflow;

[Table(Name = "WorkflowDefinition")]
public sealed class WorkflowDefinitionRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 2)] public string Code { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 3)] public string Name { get; set; } = string.Empty;
    [Column(StringLength = 1000, IsNullable = false, Position = 4)] public string Description { get; set; } = string.Empty;
    [Column(Position = 5, IsNullable = false)] public int VersionNumber { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 6, IsNullable = false)] public WorkflowDefinitionStatus Status { get; set; }
    [Column(Position = 7, IsNullable = false, ServerTime = DateTimeKind.Local)] public DateTime CreatedAt { get; set; }
    [Column(Position = 8, ServerTime = DateTimeKind.Local)] public DateTime? PublishedAt { get; set; }
    [Column(DbType = "text", IsNullable = false, Position = 9)] public string NodesJson { get; set; } = "[]";
    [Column(DbType = "text", IsNullable = false, Position = 10)] public string ConnectionsJson { get; set; } = "[]";
}
