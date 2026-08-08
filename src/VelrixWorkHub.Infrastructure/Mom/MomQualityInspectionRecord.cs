using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomQualityInspection")]
[Index("MomQualityInspection_uk_InspectionNo", "InspectionNo", true)]
public sealed class MomQualityInspectionRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid WorkOrderId { get; set; }
    [Column(Position = 3, IsNullable = true)] public Guid? OperationId { get; set; }
    [Column(Position = 4, IsNullable = true)] public Guid? ProductId { get; set; }
    [Column(Position = 5, IsNullable = true)] public Guid? StandardId { get; set; }
    [Column(Position = 6, StringLength = 80)] public string? StandardCode { get; set; }
    [Column(Position = 7, StringLength = 50)] public string? StandardVersion { get; set; }
    [Column(StringLength = -1, Position = 8, IsNullable = false)] public string StandardSnapshotJson { get; set; } = "{}";
    [Column(MapType = typeof(string), StringLength = 50, Position = 9, IsNullable = false)] public MomQualityInspectionType InspectionType { get; set; }
    [Column(Position = 10, IsNullable = false, StringLength = 80)] public string InspectionNo { get; set; } = string.Empty;
    [Column(Position = 11, StringLength = 100)] public string? BatchNo { get; set; }
    [Column(Position = 12, StringLength = 100)] public string? SerialNo { get; set; }
    [Column(Position = 13, IsNullable = false, DbType = "numeric(18,6)")] public decimal SampleQuantity { get; set; }
    [Column(Position = 14, IsNullable = false, DbType = "numeric(18,6)")] public decimal AcceptedQuantity { get; set; }
    [Column(Position = 15, IsNullable = false, DbType = "numeric(18,6)")] public decimal RejectedQuantity { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 16, IsNullable = false)] public MomQualityInspectionStatus Status { get; set; }
    [Column(Position = 17, StringLength = 100)] public string? Inspector { get; set; }
    [Column(Position = 18)] public DateTime? InspectedOn { get; set; }
    [Column(Position = 19, IsNullable = false)] public DateTime CreatedOn { get; set; }
    [Column(Position = 20, StringLength = 500)] public string? Notes { get; set; }
    [Column(StringLength = -1, Position = 21, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}
