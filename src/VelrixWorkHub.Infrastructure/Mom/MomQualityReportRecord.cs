using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomQualityReport")]
[Index("MomQualityReport_uk_ReportNo", "ReportNo", true)]
[Index("MomQualityReport_uk_InspectionId", "InspectionId", true)]
public sealed class MomQualityReportRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid InspectionId { get; set; }
    [Column(Position = 3, IsNullable = false)] public Guid WorkOrderId { get; set; }
    [Column(Position = 4, IsNullable = true)] public Guid? OperationId { get; set; }
    [Column(Position = 5, IsNullable = true)] public Guid? ProductId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 6, IsNullable = false)] public MomQualityInspectionType InspectionType { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 7, IsNullable = false)] public MomQualityInspectionStatus InspectionStatus { get; set; }
    [Column(Position = 8, StringLength = 80, IsNullable = false)] public string ReportNo { get; set; } = string.Empty;
    [Column(Position = 9, StringLength = 80, IsNullable = false)] public string InspectionNo { get; set; } = string.Empty;
    [Column(Position = 10, StringLength = 80)] public string? StandardCode { get; set; }
    [Column(Position = 11, StringLength = 50)] public string? StandardVersion { get; set; }
    [Column(Position = 12, StringLength = 100)] public string? BatchNo { get; set; }
    [Column(Position = 13, StringLength = 100)] public string? SerialNo { get; set; }
    [Column(Position = 14, IsNullable = false, DbType = "numeric(18,6)")] public decimal SampleQuantity { get; set; }
    [Column(Position = 15, IsNullable = false, DbType = "numeric(18,6)")] public decimal AcceptedQuantity { get; set; }
    [Column(Position = 16, IsNullable = false, DbType = "numeric(18,6)")] public decimal RejectedQuantity { get; set; }
    [Column(Position = 17, StringLength = 50, IsNullable = false)] public string Conclusion { get; set; } = string.Empty;
    [Column(StringLength = -1, Position = 18, IsNullable = false)] public string SnapshotJson { get; set; } = "{}";
    [Column(MapType = typeof(string), StringLength = 50, Position = 19, IsNullable = false)] public MomQualityReportStatus Status { get; set; }
    [Column(Position = 20, StringLength = 100, IsNullable = false)] public string CreatedBy { get; set; } = string.Empty;
    [Column(Position = 21, IsNullable = false)] public DateTime CreatedOn { get; set; }
    [Column(Position = 22, StringLength = 100)] public string? PublishedBy { get; set; }
    [Column(Position = 23)] public DateTime? PublishedOn { get; set; }
    [Column(Position = 24, StringLength = 100)] public string? VoidedBy { get; set; }
    [Column(Position = 25)] public DateTime? VoidedOn { get; set; }
    [Column(Position = 26, StringLength = 500)] public string? Notes { get; set; }
    [Column(StringLength = -1, Position = 27, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}
