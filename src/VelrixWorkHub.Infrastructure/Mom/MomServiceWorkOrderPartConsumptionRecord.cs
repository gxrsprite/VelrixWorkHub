using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomServiceWorkOrderPartConsumption")]
[Index("MomServiceWorkOrderPartConsumption_uk_SourceNo", "SourceNo", true)]
[Index("MomServiceWorkOrderPartConsumption_ix_ServiceWorkOrderId_ConsumedOn", "ServiceWorkOrderId,ConsumedOn", false)]
public sealed class MomServiceWorkOrderPartConsumptionRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid ServiceWorkOrderId { get; set; }
    [Column(Position = 3, IsNullable = false)] public Guid EquipmentId { get; set; }
    [Column(Position = 4, IsNullable = false)] public Guid ProductId { get; set; }
    [Column(Position = 5, IsNullable = false)] public Guid WarehouseId { get; set; }
    [Column(Position = 6, IsNullable = true)] public Guid? LocationId { get; set; }
    [Column(Position = 7, DbType = "numeric(12,2)", IsNullable = false)] public decimal Quantity { get; set; }
    [Column(Position = 8, StringLength = 80, IsNullable = false)] public string SourceNo { get; set; } = string.Empty;
    [Column(Position = 9, DbType = "date", IsNullable = false)] public DateTime ConsumedOn { get; set; }
    [Column(Position = 10, StringLength = 80, IsNullable = true)] public string? BatchNo { get; set; }
    [Column(Position = 11, DbType = "date", IsNullable = true)] public DateTime? ExpiryDate { get; set; }
    [Column(Position = 12, StringLength = 80, IsNullable = true)] public string? SerialNo { get; set; }
    [Column(Position = 13, StringLength = 100, IsNullable = false)] public string Actor { get; set; } = string.Empty;
    [Column(Position = 14, StringLength = 500, IsNullable = true)] public string? Notes { get; set; }
    [Column(Position = 15, StringLength = -1, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}
