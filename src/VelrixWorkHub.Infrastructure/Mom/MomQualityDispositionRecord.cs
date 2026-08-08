using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomQualityDisposition")]
[Index("MomQualityDisposition_uk_NonconformanceId", "NonconformanceId", true)]
[Index("MomQualityDisposition_uk_SourceNo", "SourceNo", true)]
public sealed class MomQualityDispositionRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid NonconformanceId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 3, IsNullable = false)] public MomQualityDispositionAction Action { get; set; }
    [Column(Position = 4, IsNullable = false, DbType = "numeric(18,6)")] public decimal Quantity { get; set; }
    [Column(Position = 5, IsNullable = true)] public Guid? TargetWorkOrderId { get; set; }
    [Column(Position = 6, IsNullable = true)] public Guid? TargetOperationId { get; set; }
    [Column(Position = 7, IsNullable = false, StringLength = 80)] public string SourceNo { get; set; } = string.Empty;
    [Column(MapType = typeof(string), StringLength = 50, Position = 8, IsNullable = false)] public MomQualityDispositionStatus Status { get; set; }
    [Column(Position = 9, IsNullable = false)] public DateTime CreatedOn { get; set; }
    [Column(Position = 10, IsNullable = true)] public DateTime? CompletedOn { get; set; }
    [Column(Position = 11, StringLength = 100)] public string? CompletedBy { get; set; }
    [Column(Position = 12, StringLength = 500)] public string? Notes { get; set; }
    [Column(StringLength = -1, Position = 13, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}
