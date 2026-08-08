using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomManufacturingVersion")]
public sealed class MomManufacturingVersionRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid ProductId { get; set; }
    [Column(StringLength = 80, IsNullable = false, Position = 3)] public string VersionCode { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 4)] public string Name { get; set; } = string.Empty;
    [Column(Position = 5, DbType = "date", IsNullable = false)] public DateTime EffectiveFrom { get; set; }
    [Column(Position = 6, DbType = "date")] public DateTime? EffectiveTo { get; set; }
    [Column(StringLength = 200, Position = 7)] public string? EngineeringChangeReference { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 8, IsNullable = false)] public MomManufacturingVersionStatus Status { get; set; }
    [Column(StringLength = -1, Position = 9, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}
