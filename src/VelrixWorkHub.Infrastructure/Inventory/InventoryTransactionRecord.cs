using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Inventory;
[Table(Name = "ErpInventoryTransaction")]
public sealed class InventoryTransactionRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2)] public Guid ProductId { get; set; }
    [Column(Position = 3)] public Guid WarehouseId { get; set; }
    [Column(Position = 4)] public Guid? LocationId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 5)] public InventoryTransactionKind Kind { get; set; }
    [Column(Position = 6, DbType = "numeric(12,2)")] public decimal Quantity { get; set; }
    [Column(StringLength = 80, IsNullable = false, Position = 7)] public string SourceNo { get; set; } = string.Empty;
    [Column(Position = 8, DbType = "date", IsNullable = false)] public DateTime OccurredOn { get; set; }
    [Column(StringLength = 500, Position = 9)] public string? Notes { get; set; }
    [Column(StringLength = 80, Position = 10)] public string? BatchNo { get; set; }
    [Column(Position = 11, DbType = "date")] public DateTime? ExpiryDate { get; set; }
    [Column(StringLength = 80, Position = 12)] public string? SerialNo { get; set; }
}
