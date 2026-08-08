using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomEquipment")]
[Index("MomEquipment_uk_WorkCenter_Code", "WorkCenterId,Code", true)]
public sealed class MomEquipmentRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid WorkCenterId { get; set; }
    [Column(Position = 3, IsNullable = false, StringLength = 80)] public string Code { get; set; } = string.Empty;
    [Column(Position = 4, IsNullable = false, StringLength = 200)] public string Name { get; set; } = string.Empty;
    [Column(Position = 5, StringLength = 200, IsNullable = true)] public string? Model { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 6, IsNullable = false)] public MomMasterDataStatus Status { get; set; }
    [Column(StringLength = -1, Position = 7, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}
