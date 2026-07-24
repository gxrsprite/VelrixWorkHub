using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Assets;

[Table(Name = "OaAssetOperation")]
[Index("OaAssetOperation_ix_AssetOccurredAt", nameof(AssetId) + "," + nameof(OccurredAt))]
public sealed class OaAssetOperationRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid AssetId { get; set; }
    [Column(IsNullable = true, Position = 3)] public Guid? AssignmentId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 4)] public OaAssetOperationKind Kind { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = true, Position = 5)] public OaAssetStatus? FromStatus { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = true, Position = 6)] public OaAssetStatus? ToStatus { get; set; }
    [Column(IsNullable = true, Position = 7)] public Guid? RelatedUserId { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 8)] public string ActorName { get; set; } = string.Empty;
    [Column(StringLength = 1000, IsNullable = true, Position = 9)] public string? Note { get; set; }
    [Column(IsNullable = false, ServerTime = DateTimeKind.Local, Position = 10)] public DateTime OccurredAt { get; set; }
}
