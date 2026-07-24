using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Assets;

[Table(Name = "OaAsset")]
[Index("OaAsset_uk_AssetNo", nameof(AssetNo), true)]
public sealed class OaAssetRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 80, IsNullable = false, Position = 2)] public string AssetNo { get; set; } = string.Empty;
    [Column(StringLength = 100, IsNullable = false, Position = 3)] public string Category { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 4)] public string Name { get; set; } = string.Empty;
    [Column(StringLength = 120, Position = 5)] public string? SerialNo { get; set; }
    [Column(Position = 6)] public Guid? ResponsibleUserId { get; set; }
    [Column(StringLength = 200, Position = 7)] public string? Location { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 8)] public OaAssetStatus Status { get; set; }
    [Column(StringLength = -1, IsNullable = false, Position = 9)] public string OtherInfo { get; set; } = "{}";
    [Column(Position = 10, ServerTime = DateTimeKind.Local)] public DateTime CreatedAt { get; set; }
    [Column(Position = 11, ServerTime = DateTimeKind.Local)] public DateTime UpdatedAt { get; set; }
}
