using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Products;
[Table(Name = "ErpProduct")]
public sealed class ProductRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 80, IsNullable = false, Position = 2)] public string Code { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 3)] public string Name { get; set; } = string.Empty;
    [Column(StringLength = 30, IsNullable = false, Position = 4)] public string Unit { get; set; } = string.Empty;
    [Column(Position = 5)] public decimal? SalePrice { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 6)] public ProductStatus Status { get; set; }
    [Column(StringLength = 4000, Position = 7)] public string? Notes { get; set; }
    [Column(Position = 8, ServerTime = DateTimeKind.Local)] public DateTime CreatedTime { get; set; }
    [Column(Position = 9, ServerTime = DateTimeKind.Local)] public DateTime ModifiedTime { get; set; }
    [Column(Position = 10)] public decimal? MaxPurchaseQuantity { get; set; }
    [Column(Position = 11)] public decimal? SafetyStock { get; set; }
    [Column(StringLength = -1, IsNullable = false, Position = 12)] public string OtherInfo { get; set; } = "{}";
}
