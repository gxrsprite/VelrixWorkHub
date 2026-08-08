using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomQualityNonconformance")]
[Index("MomQualityNonconformance_uk_NonconformanceNo", "NonconformanceNo", true)]
[Index("MomQualityNonconformance_uk_InspectionId", "InspectionId", true)]
public sealed class MomQualityNonconformanceRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid InspectionId { get; set; }
    [Column(Position = 3, IsNullable = false)] public Guid WorkOrderId { get; set; }
    [Column(Position = 4, IsNullable = true)] public Guid? OperationId { get; set; }
    [Column(Position = 5, IsNullable = true)] public Guid? ProductId { get; set; }
    [Column(Position = 6, StringLength = 100)] public string? BatchNo { get; set; }
    [Column(Position = 7, IsNullable = false, StringLength = 80)] public string NonconformanceNo { get; set; } = string.Empty;
    [Column(Position = 8, IsNullable = false, StringLength = 80)] public string DefectCode { get; set; } = string.Empty;
    [Column(Position = 9, IsNullable = false, StringLength = 1000)] public string Description { get; set; } = string.Empty;
    [Column(Position = 10, IsNullable = false, DbType = "numeric(18,6)")] public decimal Quantity { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 11, IsNullable = false)] public MomQualityNonconformanceSeverity Severity { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 12, IsNullable = false)] public MomQualityNonconformanceStatus Status { get; set; }
    [Column(Position = 13, IsNullable = true)] public Guid? DispositionId { get; set; }
    [Column(Position = 14, IsNullable = false)] public DateTime CreatedOn { get; set; }
    [Column(Position = 15, IsNullable = true)] public DateTime? ClosedOn { get; set; }
    [Column(Position = 16, StringLength = 100)] public string? ClosedBy { get; set; }
    [Column(Position = 17, StringLength = 500)] public string? ClosureNotes { get; set; }
    [Column(StringLength = -1, Position = 18, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}
