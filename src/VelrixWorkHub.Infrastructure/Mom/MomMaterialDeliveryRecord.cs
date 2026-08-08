using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomMaterialDelivery")]
[Index("MomMaterialDelivery_uk_SourceNo", "SourceNo", true)]
public sealed class MomMaterialDeliveryRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid RequirementId { get; set; }
    [Column(Position = 3, IsNullable = false)] public Guid WorkOrderId { get; set; }
    [Column(Position = 4, IsNullable = false)] public Guid ProductId { get; set; }
    [Column(Position = 5, IsNullable = false)] public Guid WorkCenterId { get; set; }
    [Column(Position = 6)] public Guid? SourceWarehouseId { get; set; }
    [Column(Position = 7)] public Guid? SourceLocationId { get; set; }
    [Column(Position = 8)] public Guid? TargetWarehouseId { get; set; }
    [Column(Position = 9)] public Guid? TargetLocationId { get; set; }
    [Column(Position = 10, IsNullable = false, DbType = "numeric(18,6)")] public decimal Quantity { get; set; }
    [Column(Position = 11, IsNullable = false, StringLength = 80)] public string SourceNo { get; set; } = string.Empty;
    [Column(Position = 12, IsNullable = false)] public DateOnly OccurredOn { get; set; }
    [Column(Position = 13, StringLength = 500)] public string? Notes { get; set; }
    [Column(StringLength = -1, Position = 14, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
    [Column(Position = 15, StringLength = 80)] public string? BatchNo { get; set; }
    [Column(Position = 16, DbType = "date")] public DateOnly? ExpiryDate { get; set; }
    [Column(Position = 17, StringLength = 80)] public string? SerialNo { get; set; }
}
