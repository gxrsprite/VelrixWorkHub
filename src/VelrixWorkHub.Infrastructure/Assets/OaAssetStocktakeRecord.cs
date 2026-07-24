using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Assets;

[Table(Name = "OaAssetStocktake")]
[Index("OaAssetStocktake_ix_AssetStocktakenAt", nameof(AssetId) + "," + nameof(StocktakenAt))]
public sealed class OaAssetStocktakeRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid AssetId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 3)] public OaAssetStatus ExpectedStatus { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = true, Position = 4)] public OaAssetStatus? ActualStatus { get; set; }
    [Column(IsNullable = true, Position = 5)] public Guid? ExpectedResponsibleUserId { get; set; }
    [Column(IsNullable = true, Position = 6)] public Guid? ActualResponsibleUserId { get; set; }
    [Column(StringLength = 500, IsNullable = true, Position = 7)] public string? ExpectedLocation { get; set; }
    [Column(StringLength = 500, IsNullable = true, Position = 8)] public string? ActualLocation { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 9)] public OaAssetStocktakeResult Result { get; set; }
    [Column(StringLength = 2000, IsNullable = true, Position = 10)] public string? Reason { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 11)] public string ActorName { get; set; } = string.Empty;
    [Column(StringLength = -1, IsNullable = false, Position = 12)] public string OtherInfo { get; set; } = "{}";
    [Column(IsNullable = false, ServerTime = DateTimeKind.Local, Position = 13)] public DateTime StocktakenAt { get; set; }
    [Column(StringLength = 2000, IsNullable = true, Position = 14)] public string? Resolution { get; set; }
    [Column(StringLength = 100, IsNullable = true, Position = 15)] public string? ResolvedBy { get; set; }
    [Column(IsNullable = true, Position = 16)] public DateTime? ResolvedAt { get; set; }
}
