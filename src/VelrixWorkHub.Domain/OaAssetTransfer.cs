namespace VelrixWorkHub.Domain;

/// <summary>资产责任人/存放位置转移的不可变记录。</summary>
public sealed class OaAssetTransfer
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid AssetId { get; private set; }
    public Guid? FromUserId { get; private set; }
    public Guid? ToUserId { get; private set; }
    public string? FromLocation { get; private set; }
    public string? ToLocation { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string ActorName { get; private set; } = string.Empty;
    public DateTime TransferredAt { get; private set; }

    public OaAssetTransfer(Guid assetId, Guid? fromUserId, Guid? toUserId, string? fromLocation, string? toLocation,
        string reason, string actorName, DateTime transferredAt)
    {
        if (assetId == Guid.Empty) throw new ArgumentException("资产不能为空。", nameof(assetId));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("转移原因不能为空。", nameof(reason));
        AssetId = assetId;
        FromUserId = fromUserId;
        ToUserId = toUserId;
        FromLocation = Clean(fromLocation);
        ToLocation = Clean(toLocation);
        Reason = reason.Trim();
        ActorName = string.IsNullOrWhiteSpace(actorName) ? "system" : actorName.Trim();
        TransferredAt = transferredAt;
    }

    public static OaAssetTransfer Restore(Guid id, Guid assetId, Guid? fromUserId, Guid? toUserId, string? fromLocation,
        string? toLocation, string reason, string actorName, DateTime transferredAt)
    {
        var item = new OaAssetTransfer(assetId, fromUserId, toUserId, fromLocation, toLocation, reason, actorName, transferredAt) { Id = id };
        return item;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
