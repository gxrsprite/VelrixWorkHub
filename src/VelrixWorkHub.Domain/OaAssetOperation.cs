namespace VelrixWorkHub.Domain;

public enum OaAssetOperationKind
{
    Created,
    Edited,
    Assigned,
    Returned,
    StatusChanged,
    Transferred,
    Stocktaken,
    StocktakeResolved
}

/// <summary>资产台账的不可变操作流水，不用于替代 ERP 固定资产核算。</summary>
public sealed class OaAssetOperation
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid AssetId { get; private set; }
    public Guid? AssignmentId { get; private set; }
    public OaAssetOperationKind Kind { get; private set; }
    public OaAssetStatus? FromStatus { get; private set; }
    public OaAssetStatus? ToStatus { get; private set; }
    public Guid? RelatedUserId { get; private set; }
    public string ActorName { get; private set; } = string.Empty;
    public string? Note { get; private set; }
    public DateTime OccurredAt { get; private set; }

    public OaAssetOperation(Guid assetId, OaAssetOperationKind kind, Guid? assignmentId, OaAssetStatus? fromStatus,
        OaAssetStatus? toStatus, Guid? relatedUserId, string? actorName, string? note, DateTime occurredAt)
    {
        if (assetId == Guid.Empty) throw new ArgumentException("资产不能为空。", nameof(assetId));
        AssetId = assetId;
        AssignmentId = assignmentId;
        Kind = kind;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        RelatedUserId = relatedUserId;
        ActorName = string.IsNullOrWhiteSpace(actorName) ? "system" : actorName.Trim();
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        OccurredAt = occurredAt;
    }

    public static OaAssetOperation Restore(Guid id, Guid assetId, OaAssetOperationKind kind, Guid? assignmentId,
        OaAssetStatus? fromStatus, OaAssetStatus? toStatus, Guid? relatedUserId, string actorName, string? note, DateTime occurredAt)
    {
        var item = new OaAssetOperation(assetId, kind, assignmentId, fromStatus, toStatus, relatedUserId, actorName, note, occurredAt);
        item.Id = id;
        return item;
    }
}
