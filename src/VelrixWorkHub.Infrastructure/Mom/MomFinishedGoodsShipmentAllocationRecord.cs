using FreeSql.DataAnnotations;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomFinishedGoodsShipmentAllocation")]
[Index("MomFinishedGoodsShipmentAllocation_uk_SourceNo", "SourceNo", true)]
public sealed class MomFinishedGoodsShipmentAllocationRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid ShipmentId { get; set; }
    [Column(Position = 3, IsNullable = false)] public Guid FinishedGoodsReceiptId { get; set; }
    [Column(Position = 4, IsNullable = false)] public Guid ProductId { get; set; }
    [Column(Position = 5, IsNullable = false)] public Guid WarehouseId { get; set; }
    [Column(Position = 6, IsNullable = true)] public Guid? LocationId { get; set; }
    [Column(Position = 7, IsNullable = false, DbType = "numeric(18,6)")] public decimal Quantity { get; set; }
    [Column(Position = 8, StringLength = 80, IsNullable = false)] public string SourceNo { get; set; } = string.Empty;
    [Column(Position = 9, IsNullable = false)] public DateTime ShipmentDate { get; set; }
    [Column(Position = 10, StringLength = 100)] public string? BatchNo { get; set; }
    [Column(Position = 11)] public DateTime? ExpiryDate { get; set; }
    [Column(Position = 12, StringLength = 100)] public string? SerialNo { get; set; }
    [Column(StringLength = -1, Position = 13, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}
