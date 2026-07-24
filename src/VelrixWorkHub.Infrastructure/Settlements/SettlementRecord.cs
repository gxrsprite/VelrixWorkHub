using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Settlements;

[Table(Name = "ErpSettlement")]
public sealed class SettlementRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 80, IsNullable = false, Position = 2)] public string ReferenceNo { get; set; } = string.Empty;
    [Column(Position = 3, IsNullable = false)] public Guid OrderId { get; set; }
    [Column(Position = 4, IsNullable = false)] public Guid PartyId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 5, IsNullable = false)] public ErpSettlementKind Kind { get; set; }
    [Column(Position = 6, IsNullable = false, DbType = "numeric(12,2)")] public decimal Amount { get; set; }
    [Column(Position = 7, DbType = "date", IsNullable = false)] public DateTime OccurredOn { get; set; }
    [Column(StringLength = 240, IsNullable = false, Position = 8)] public string Notes { get; set; } = string.Empty;
    [Column(MapType = typeof(string), StringLength = 50, Position = 9, IsNullable = false)] public ErpSettlementStatus Status { get; set; }
    [Column(StringLength = 240, IsNullable = false, Position = 10)] public string VoidReason { get; set; } = string.Empty;
}
