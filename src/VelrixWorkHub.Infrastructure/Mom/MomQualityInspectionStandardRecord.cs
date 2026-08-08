using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomQualityInspectionStandard")]
[Index("MomQualityInspectionStandard_uk_Type_Product_Code_Version", "InspectionType,ProductId,Code,Version", true)]
public sealed class MomQualityInspectionStandardRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = true)] public Guid? ProductId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 3, IsNullable = false)] public MomQualityInspectionType InspectionType { get; set; }
    [Column(Position = 4, IsNullable = false, StringLength = 80)] public string Code { get; set; } = string.Empty;
    [Column(Position = 5, IsNullable = false, StringLength = 200)] public string Name { get; set; } = string.Empty;
    [Column(Position = 6, IsNullable = false, StringLength = 50)] public string Version { get; set; } = string.Empty;
    [Column(MapType = typeof(string), StringLength = 50, Position = 7, IsNullable = false)] public MomQualityInspectionStandardStatus Status { get; set; }
    [Column(StringLength = -1, Position = 8, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}
