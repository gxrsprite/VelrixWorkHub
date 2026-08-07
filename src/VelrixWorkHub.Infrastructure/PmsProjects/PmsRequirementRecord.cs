using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmsProjects;

[Table(Name = "PmsRequirement")]
public sealed class PmsRequirementRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid ProjectId { get; set; }
    [Column(Position = 3, IsNullable = true)] public Guid? ProductId { get; set; }
    [Column(Position = 4, IsNullable = true)] public Guid? BaselineId { get; set; }
    [Column(StringLength = 80, IsNullable = false, Position = 5)] public string RequirementNo { get; set; } = string.Empty;
    [Column(Position = 6)] public bool IsHighlighted { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 7)] public string Proposer { get; set; } = string.Empty;
    [Column(StringLength = 100, IsNullable = true, Position = 8)] public string? OwnerName { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 9)] public PmsRequirementPriority Priority { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 10)] public PmsRequirementStatus Status { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 11)] public PmsRequirementType RequirementType { get; set; }
    [Column(Position = 12, DbType = "date", IsNullable = false)] public DateTime ProposedDate { get; set; }
    [Column(Position = 13, DbType = "date", IsNullable = true)] public DateTime? DesiredCompletionDate { get; set; }
    [Column(Position = 14, DbType = "date", IsNullable = true)] public DateTime? PlannedCompletionDate { get; set; }
    [Column(StringLength = 4000, IsNullable = false, Position = 15)] public string Description { get; set; } = string.Empty;
    [Column(StringLength = 4000, IsNullable = true, Position = 16)] public string? BackgroundValue { get; set; }
    [Column(StringLength = 1000, IsNullable = false, Position = 17)] public string OtherInfo { get; set; } = "{}";
    [Column(Position = 18, ServerTime = DateTimeKind.Local)] public DateTime CreatedTime { get; set; }
    [Column(Position = 19, ServerTime = DateTimeKind.Local)] public DateTime ModifiedTime { get; set; }
}
