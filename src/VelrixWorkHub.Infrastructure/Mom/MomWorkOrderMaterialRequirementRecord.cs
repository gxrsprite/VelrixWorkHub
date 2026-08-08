using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomWorkOrderMaterialRequirement")]
[Index("MomWorkOrderMaterialRequirement_uk_WorkOrder_Line", "WorkOrderId,LineNo", true)]
public sealed class MomWorkOrderMaterialRequirementRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid WorkOrderId { get; set; }
    [Column(Position = 3, IsNullable = false)] public Guid ManufacturingVersionId { get; set; }
    [Column(Position = 4, IsNullable = false)] public int LineNo { get; set; }
    [Column(Position = 5, IsNullable = false)] public Guid ComponentProductId { get; set; }
    [Column(Position = 6, IsNullable = false, DbType = "numeric(18,6)")] public decimal RequiredQuantity { get; set; }
    [Column(Position = 7, IsNullable = false, DbType = "numeric(18,6)")] public decimal IssuedQuantity { get; set; }
    [Column(Position = 8, IsNullable = false, DbType = "numeric(18,6)")] public decimal ReturnedQuantity { get; set; }
    [Column(StringLength = -1, Position = 9, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}
