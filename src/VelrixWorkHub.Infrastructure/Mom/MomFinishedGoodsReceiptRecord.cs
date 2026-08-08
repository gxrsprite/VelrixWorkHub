using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomFinishedGoodsReceipt")]
[Index("MomFinishedGoodsReceipt_uk_SourceNo", "SourceNo", true)]
public sealed class MomFinishedGoodsReceiptRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid WorkOrderId { get; set; }
    [Column(Position = 3, IsNullable = false)] public Guid ProductId { get; set; }
    [Column(Position = 4, IsNullable = false)] public Guid WarehouseId { get; set; }
    [Column(Position = 5, IsNullable = true)] public Guid? LocationId { get; set; }
    [Column(Position = 6, IsNullable = false, DbType = "numeric(18,6)")] public decimal Quantity { get; set; }
    [Column(Position = 7, StringLength = 80, IsNullable = false)] public string SourceNo { get; set; } = string.Empty;
    [Column(Position = 8, IsNullable = false)] public DateTime ReceiptDate { get; set; }
    [Column(Position = 9, StringLength = 100)] public string? BatchNo { get; set; }
    [Column(Position = 10)] public DateTime? ExpiryDate { get; set; }
    [Column(Position = 11, StringLength = 100)] public string? SerialNo { get; set; }
    [Column(StringLength = -1, Position = 12, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}
