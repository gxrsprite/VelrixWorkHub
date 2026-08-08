using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomMaterialMovement")]
[Index("MomMaterialMovement_uk_SourceNo", nameof(SourceNo), true)]
public sealed class MomMaterialMovementRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid RequirementId { get; set; }
    [Column(Position = 3, IsNullable = false)] public Guid WorkOrderId { get; set; }
    [Column(Position = 4, IsNullable = false)] public Guid ProductId { get; set; }
    [Column(Position = 5, IsNullable = false)] public Guid WarehouseId { get; set; }
    [Column(Position = 6)] public Guid? LocationId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 7, IsNullable = false)] public MomMaterialMovementKind Kind { get; set; }
    [Column(Position = 8, IsNullable = false, DbType = "numeric(18,6)")] public decimal Quantity { get; set; }
    [Column(StringLength = 80, IsNullable = false, Position = 9)] public string SourceNo { get; set; } = string.Empty;
    [Column(Position = 10, DbType = "date", IsNullable = false)] public DateTime OccurredOn { get; set; }
    [Column(StringLength = 500, Position = 11)] public string? Notes { get; set; }
    [Column(StringLength = 80, Position = 12)] public string? BatchNo { get; set; }
    [Column(Position = 13, DbType = "date")] public DateTime? ExpiryDate { get; set; }
    [Column(StringLength = 80, Position = 14)] public string? SerialNo { get; set; }
    [Column(StringLength = -1, Position = 15, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}
