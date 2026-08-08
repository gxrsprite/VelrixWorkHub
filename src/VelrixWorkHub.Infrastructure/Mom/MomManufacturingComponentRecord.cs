using FreeSql.DataAnnotations;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomManufacturingComponent")]
public sealed class MomManufacturingComponentRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid ManufacturingVersionId { get; set; }
    [Column(Position = 3, IsNullable = false)] public int LineNo { get; set; }
    [Column(Position = 4, IsNullable = false)] public Guid ComponentProductId { get; set; }
    [Column(Position = 5, IsNullable = false, DbType = "numeric(18,6)")] public decimal QuantityPer { get; set; }
    [Column(Position = 6, IsNullable = false, DbType = "numeric(8,4)")] public decimal ScrapRatePercent { get; set; }
    [Column(Position = 7, IsNullable = false)] public int OperationSequence { get; set; }
    [Column(StringLength = 1000, Position = 8)] public string? Notes { get; set; }
    [Column(StringLength = -1, Position = 9, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}
