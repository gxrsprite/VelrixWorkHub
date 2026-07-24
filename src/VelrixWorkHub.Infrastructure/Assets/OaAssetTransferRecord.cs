using FreeSql.DataAnnotations;

namespace VelrixWorkHub.Infrastructure.Assets;

[Table(Name = "OaAssetTransfer")]
[Index("OaAssetTransfer_ix_AssetTransferredAt", nameof(AssetId) + "," + nameof(TransferredAt))]
public sealed class OaAssetTransferRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid AssetId { get; set; }
    [Column(IsNullable = true, Position = 3)] public Guid? FromUserId { get; set; }
    [Column(IsNullable = true, Position = 4)] public Guid? ToUserId { get; set; }
    [Column(StringLength = 500, IsNullable = true, Position = 5)] public string? FromLocation { get; set; }
    [Column(StringLength = 500, IsNullable = true, Position = 6)] public string? ToLocation { get; set; }
    [Column(StringLength = 2000, IsNullable = false, Position = 7)] public string Reason { get; set; } = string.Empty;
    [Column(StringLength = 100, IsNullable = false, Position = 8)] public string ActorName { get; set; } = string.Empty;
    [Column(IsNullable = false, ServerTime = DateTimeKind.Local, Position = 9)] public DateTime TransferredAt { get; set; }
}
