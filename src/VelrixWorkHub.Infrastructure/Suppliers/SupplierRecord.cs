using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Suppliers;
[Table(Name = "ErpSupplier")]
public sealed class SupplierRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 80, IsNullable = false, Position = 2)] public string Code { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 3)] public string Name { get; set; } = string.Empty;
    [Column(StringLength = 100, Position = 4)] public string? ContactName { get; set; }
    [Column(StringLength = 50, Position = 5)] public string? Phone { get; set; }
    [Column(StringLength = 4000, Position = 6)] public string? Notes { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 7)] public SupplierStatus Status { get; set; }
    [Column(Position = 8, ServerTime = DateTimeKind.Local)] public DateTime CreatedTime { get; set; }
    [Column(Position = 9, ServerTime = DateTimeKind.Local)] public DateTime ModifiedTime { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 10)] public SupplierQualificationStatus QualificationStatus { get; set; }
    [Column(StringLength = -1, IsNullable = false, Position = 11)] public string OtherInfo { get; set; } = "{}";
}
