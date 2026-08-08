using FreeSql.DataAnnotations;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomMaterialConsumptionAllocation")]
[Index("MomMaterialConsumptionAllocation_uk_SourceNo", "SourceNo", true)]
public sealed class MomMaterialConsumptionAllocationRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid ConsumptionId { get; set; }
    [Column(Position = 3, IsNullable = false)] public Guid DeliveryId { get; set; }
    [Column(Position = 4, IsNullable = false)] public Guid RequirementId { get; set; }
    [Column(Position = 5, IsNullable = false)] public Guid WorkOrderId { get; set; }
    [Column(Position = 6, IsNullable = false)] public Guid ProductId { get; set; }
    [Column(Position = 7, IsNullable = false)] public Guid WorkCenterId { get; set; }
    [Column(Position = 8, IsNullable = false, DbType = "numeric(18,6)")] public decimal Quantity { get; set; }
    [Column(Position = 9, StringLength = 100)] public string? BatchNo { get; set; }
    [Column(Position = 10, IsNullable = true)] public DateOnly? ExpiryDate { get; set; }
    [Column(Position = 11, StringLength = 100)] public string? SerialNo { get; set; }
    [Column(Position = 12, IsNullable = false, StringLength = 100)] public string SourceNo { get; set; } = string.Empty;
    [Column(Position = 13, IsNullable = false)] public DateOnly OccurredOn { get; set; }
    [Column(Position = 14, StringLength = 500)] public string? Notes { get; set; }
    [Column(StringLength = -1, Position = 15, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}
