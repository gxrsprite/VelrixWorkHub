using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Assets;

[Table(Name = "OaConsumableSupply")]
[Index("OaConsumableSupply_uk_Code", nameof(Code), true)]
public sealed class OaConsumableSupplyRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 80, IsNullable = false, Position = 2)] public string Code { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 3)] public string Name { get; set; } = string.Empty;
    [Column(StringLength = 50, IsNullable = false, Position = 4)] public string Unit { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = true, Position = 5)] public string? Location { get; set; }
    [Column(IsNullable = false, Position = 6)] public bool IsActive { get; set; }
    [Column(StringLength = -1, IsNullable = false, Position = 7)] public string OtherInfo { get; set; } = "{}";
    [Column(IsNullable = false, Position = 8)] public DateTime CreatedAt { get; set; }
    [Column(IsNullable = false, Position = 9)] public DateTime UpdatedAt { get; set; }
}

[Table(Name = "OaConsumableTransaction")]
[Index("OaConsumableTransaction_uk_SourceNo", nameof(SourceNo), true)]
[Index("OaConsumableTransaction_ix_SupplyOccurred", nameof(SupplyId) + "," + nameof(OccurredAt))]
public sealed class OaConsumableTransactionRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid SupplyId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 3)] public OaConsumableTransactionKind Kind { get; set; }
    [Column(IsNullable = false, Precision = 18, Scale = 4, Position = 4)] public decimal Quantity { get; set; }
    [Column(IsNullable = true, Position = 5)] public Guid? RecipientUserId { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 6)] public string SourceNo { get; set; } = string.Empty;
    [Column(StringLength = 100, IsNullable = false, Position = 7)] public string ActorName { get; set; } = string.Empty;
    [Column(StringLength = 2000, IsNullable = true, Position = 8)] public string? Notes { get; set; }
    [Column(IsNullable = false, Position = 9)] public DateTime OccurredAt { get; set; }
}
