using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Warehouses;
[Table(Name = "ErpWarehouse")]
public sealed class WarehouseRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 80, IsNullable = false, Position = 2)] public string Code { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 3)] public string Name { get; set; } = string.Empty;
    [Column(StringLength = 500, Position = 4)] public string? Address { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 5)] public WarehouseStatus Status { get; set; }
    [Column(Position = 6, ServerTime = DateTimeKind.Local)] public DateTime CreatedTime { get; set; }
    [Column(Position = 7, ServerTime = DateTimeKind.Local)] public DateTime ModifiedTime { get; set; }
    [Column(StringLength = -1, IsNullable = false, Position = 8)] public string OtherInfo { get; set; } = "{}";
}
[Table(Name = "ErpWarehouseLocation")]
public sealed class WarehouseLocationRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2)] public Guid WarehouseId { get; set; }
    [Column(StringLength = 80, IsNullable = false, Position = 3)] public string Code { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 4)] public string Name { get; set; } = string.Empty;
}
