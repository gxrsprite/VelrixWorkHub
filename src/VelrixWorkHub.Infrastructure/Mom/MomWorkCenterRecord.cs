using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomWorkCenter")]
public sealed class MomWorkCenterRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid FactoryId { get; set; }
    [Column(StringLength = 80, IsNullable = false, Position = 3)] public string Code { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 4)] public string Name { get; set; } = string.Empty;
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 5)] public MomWorkCenterType Type { get; set; }
    [Column(StringLength = 200, Position = 6)] public string? ProductionLineName { get; set; }
    [Column(IsNullable = false, DbType = "numeric(8,2)", Position = 7)] public decimal StandardHoursPerDay { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 8)] public MomMasterDataStatus Status { get; set; }
    [Column(StringLength = -1, IsNullable = false, Position = 9)] public string OtherInfo { get; set; } = "{}";
}
