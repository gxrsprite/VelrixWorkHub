using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomFactory")]
public sealed class MomFactoryRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 80, IsNullable = false, Position = 2)] public string Code { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 3)] public string Name { get; set; } = string.Empty;
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 4)] public MomMasterDataStatus Status { get; set; }
    [Column(StringLength = -1, IsNullable = false, Position = 5)] public string OtherInfo { get; set; } = "{}";
}
