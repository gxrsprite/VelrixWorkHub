namespace VelrixWorkHub.Domain;

/// <summary>资产盘点时的账面快照与实盘结果，不直接改写资产台账。</summary>
public sealed class OaAssetStocktake
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid AssetId { get; private set; }
    public OaAssetStatus ExpectedStatus { get; private set; }
    public OaAssetStatus? ActualStatus { get; private set; }
    public Guid? ExpectedResponsibleUserId { get; private set; }
    public Guid? ActualResponsibleUserId { get; private set; }
    public string? ExpectedLocation { get; private set; }
    public string? ActualLocation { get; private set; }
    public OaAssetStocktakeResult Result { get; private set; }
    public string? Reason { get; private set; }
    public string ActorName { get; private set; } = string.Empty;
    public string OtherInfo { get; private set; } = "{}";
    public DateTime StocktakenAt { get; private set; }
    public string? Resolution { get; private set; }
    public string? ResolvedBy { get; private set; }
    public DateTime? ResolvedAt { get; private set; }

    public OaAssetStocktake(Guid assetId, OaAssetStatus expectedStatus, OaAssetStatus? actualStatus,
        Guid? expectedResponsibleUserId, Guid? actualResponsibleUserId, string? expectedLocation, string? actualLocation,
        string? reason, string actorName, string? otherInfo, DateTime stocktakenAt)
    {
        if (assetId == Guid.Empty) throw new ArgumentException("资产不能为空。", nameof(assetId));
        if (string.IsNullOrWhiteSpace(actorName)) throw new ArgumentException("盘点操作者不能为空。", nameof(actorName));
        if (actualStatus == OaAssetStatus.InUse && actualResponsibleUserId is null)
            throw new InvalidOperationException("实盘为在用时必须填写实际责任人。");
        if (actualStatus is not null and not OaAssetStatus.InUse && actualResponsibleUserId is not null)
            throw new InvalidOperationException("非在用资产不能填写实际责任人。");

        AssetId = assetId;
        ExpectedStatus = expectedStatus;
        ActualStatus = actualStatus;
        ExpectedResponsibleUserId = expectedResponsibleUserId;
        ActualResponsibleUserId = actualResponsibleUserId;
        ExpectedLocation = Clean(expectedLocation);
        ActualLocation = actualStatus is null ? null : Clean(actualLocation);
        Result = actualStatus is null
            ? OaAssetStocktakeResult.Missing
            : expectedStatus == actualStatus && expectedResponsibleUserId == actualResponsibleUserId &&
              string.Equals(ExpectedLocation, ActualLocation, StringComparison.Ordinal)
                ? OaAssetStocktakeResult.Matched
                : OaAssetStocktakeResult.Difference;
        if (Result != OaAssetStocktakeResult.Matched && string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("盘点存在差异时必须填写原因。", nameof(reason));
        Reason = Clean(reason);
        ActorName = actorName.Trim();
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
        StocktakenAt = stocktakenAt;
    }

    public static OaAssetStocktake Restore(Guid id, Guid assetId, OaAssetStatus expectedStatus, OaAssetStatus? actualStatus,
        Guid? expectedResponsibleUserId, Guid? actualResponsibleUserId, string? expectedLocation, string? actualLocation,
        OaAssetStocktakeResult result, string? reason, string actorName, string otherInfo, DateTime stocktakenAt,
        string? resolution = null, string? resolvedBy = null, DateTime? resolvedAt = null)
    {
        var item = new OaAssetStocktake(assetId, expectedStatus, actualStatus, expectedResponsibleUserId, actualResponsibleUserId,
            expectedLocation, actualLocation, result == OaAssetStocktakeResult.Matched ? null : reason, actorName, otherInfo, stocktakenAt)
        { Id = id };
        if (item.Result != result) throw new InvalidOperationException("资产盘点结果与快照不一致。");
        var normalizedResolution = Clean(resolution);
        var normalizedResolvedBy = Clean(resolvedBy);
        if (normalizedResolution is not null || normalizedResolvedBy is not null || resolvedAt is not null)
        {
            if (normalizedResolution is null || normalizedResolvedBy is null || resolvedAt is null) throw new InvalidOperationException("资产盘点处置记录不完整。");
            item.Resolution = normalizedResolution;
            item.ResolvedBy = normalizedResolvedBy;
            item.ResolvedAt = resolvedAt;
        }
        return item;
    }

    public void Resolve(string resolution, string actorName, DateTime resolvedAt)
    {
        if (Result == OaAssetStocktakeResult.Matched) throw new InvalidOperationException("一致盘点无需处置。");
        if (ResolvedAt is not null) throw new InvalidOperationException("该盘点差异已完成处置。");
        Resolution = Required(resolution, "处置结论");
        ResolvedBy = Required(actorName, "处置操作者");
        ResolvedAt = resolvedAt;
    }

    public void RestoreResolutionForRecovery(string? resolution, string? resolvedBy, DateTime? resolvedAt)
    {
        Resolution = resolution;
        ResolvedBy = resolvedBy;
        ResolvedAt = resolvedAt;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Required(string? value, string label) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{label}不能为空。") : value.Trim();
}
