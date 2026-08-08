using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomQualityReceiptInspection")]
[Index("MomQualityReceiptInspection_uk_PurchaseOrder_Inspection", "PurchaseOrderId,InspectionId", true)]
public sealed class MomQualityReceiptInspectionRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid PurchaseOrderId { get; set; }
    [Column(Position = 3, IsNullable = false)] public Guid InspectionId { get; set; }
    [Column(Position = 4, IsNullable = false)] public Guid ProductId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 5, IsNullable = false)] public MomQualityInspectionType InspectionType { get; set; }
    [Column(Position = 6, StringLength = 80, IsNullable = false)] public string InspectionNo { get; set; } = string.Empty;
    [Column(Position = 7, StringLength = 100)] public string? BatchNo { get; set; }
    [Column(Position = 8, IsNullable = false)] public DateTime LinkedOn { get; set; }
    [Column(StringLength = -1, Position = 9, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}
