using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomMaterialDeliveryReversal")]
[Index("MomMaterialDeliveryReversal_uk_SourceNo", "SourceNo", true)]
public sealed class MomMaterialDeliveryReversalRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid DeliveryId { get; set; }
    [Column(Position = 3, IsNullable = false)] public Guid RequirementId { get; set; }
    [Column(Position = 4, IsNullable = false)] public Guid WorkOrderId { get; set; }
    [Column(Position = 5, IsNullable = false)] public Guid ProductId { get; set; }
    [Column(Position = 6, IsNullable = false)] public Guid WorkCenterId { get; set; }
    [Column(Position = 7, IsNullable = false, DbType = "numeric(18,6)")] public decimal Quantity { get; set; }
    [Column(Position = 8, IsNullable = false, StringLength = 80)] public string SourceNo { get; set; } = string.Empty;
    [Column(Position = 9, IsNullable = false)] public DateOnly OccurredOn { get; set; }
    [Column(Position = 10, StringLength = 500)] public string? Notes { get; set; }
    [Column(StringLength = -1, Position = 11, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}
