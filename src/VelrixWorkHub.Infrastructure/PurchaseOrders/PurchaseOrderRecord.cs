using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.PurchaseOrders;
[Table(Name = "PurchaseOrder")]
public sealed class PurchaseOrderRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 80, IsNullable = false, Position = 2)] public string OrderNo { get; set; } = string.Empty;
    [Column(Position = 3, IsNullable = false)] public Guid SupplierId { get; set; }
    [Column(Position = 4, IsNullable = false)] public Guid ProductId { get; set; }
    [Column(Position = 5, DbType = "date", IsNullable = false)] public DateTime OrderDate { get; set; }
    [Column(Position = 6, IsNullable = false, DbType = "numeric(12,2)")] public decimal Quantity { get; set; }
    [Column(Position = 7, IsNullable = false, DbType = "numeric(12,2)")] public decimal UnitPrice { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 8, IsNullable = false)] public PurchaseOrderStatus Status { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 9, IsNullable = false)] public PurchaseOrderSourceKind SourceKind { get; set; }
    [Column(StringLength = 80, Position = 10)] public string? SourceDocumentNo { get; set; }
    [Column(Position = 11, IsNullable = false)] public bool IsLocked { get; set; }
    [Column(Position = 12, DbType = "date")] public DateTime? DueDate { get; set; }
    [Column(Position = 13, IsNullable = true)] public Guid? SourceLineId { get; set; }
}
