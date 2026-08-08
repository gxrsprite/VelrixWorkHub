using FreeSql.DataAnnotations;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomQualityInspectionStandardItem")]
[Index("MomQualityInspectionStandardItem_uk_Standard_LineNo", "StandardId,LineNo", true)]
[Index("MomQualityInspectionStandardItem_uk_Standard_Code", "StandardId,Code", true)]
public sealed class MomQualityInspectionStandardItemRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid StandardId { get; set; }
    [Column(Position = 3, IsNullable = false)] public int LineNo { get; set; }
    [Column(Position = 4, IsNullable = false, StringLength = 80)] public string Code { get; set; } = string.Empty;
    [Column(Position = 5, IsNullable = false, StringLength = 200)] public string Name { get; set; } = string.Empty;
    [Column(Position = 6, IsNullable = false, StringLength = 500)] public string Requirement { get; set; } = string.Empty;
    [Column(Position = 7, StringLength = 50)] public string? Unit { get; set; }
    [Column(Position = 8, DbType = "numeric(18,6)")] public decimal? MinValue { get; set; }
    [Column(Position = 9, DbType = "numeric(18,6)")] public decimal? MaxValue { get; set; }
    [Column(StringLength = -1, Position = 10, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}
