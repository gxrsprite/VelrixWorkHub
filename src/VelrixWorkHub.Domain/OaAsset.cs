namespace VelrixWorkHub.Domain;

public enum OaAssetStatus
{
    Available,
    InUse,
    Maintenance,
    Retired
}

public enum OaAssetAssignmentStatus
{
    Active,
    Returned,
    Cancelled
}

public enum OaAssetStocktakeResult
{
    Matched,
    Difference,
    Missing
}

public sealed class OaAsset
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string AssetNo { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? SerialNo { get; private set; }
    public Guid? ResponsibleUserId { get; private set; }
    public string? Location { get; private set; }
    public OaAssetStatus Status { get; private set; }
    public string OtherInfo { get; private set; } = "{}";
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public OaAsset(string assetNo, string category, string name, string? serialNo, string? location, string? otherInfo, DateTime createdAt)
    {
        CreatedAt = createdAt;
        Edit(assetNo, category, name, serialNo, location, otherInfo, createdAt);
        Status = OaAssetStatus.Available;
    }

    public void Edit(string assetNo, string category, string name, string? serialNo, string? location, string? otherInfo, DateTime updatedAt)
    {
        AssetNo = Required(assetNo, "资产编号");
        Category = Required(category, "资产分类");
        Name = Required(name, "资产名称");
        SerialNo = Clean(serialNo);
        Location = Clean(location);
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
        UpdatedAt = updatedAt;
    }

    public void Assign(Guid userId, DateTime updatedAt)
    {
        if (userId == Guid.Empty) throw new ArgumentException("领用员工不能为空。", nameof(userId));
        if (Status != OaAssetStatus.Available) throw new InvalidOperationException("只有可用资产才能领用。");
        ResponsibleUserId = userId;
        Status = OaAssetStatus.InUse;
        UpdatedAt = updatedAt;
    }

    public void Return(DateTime updatedAt)
    {
        if (Status != OaAssetStatus.InUse) throw new InvalidOperationException("只有在用资产才能归还。");
        ResponsibleUserId = null;
        Status = OaAssetStatus.Available;
        UpdatedAt = updatedAt;
    }

    public void Transfer(Guid? responsibleUserId, string? location, DateTime updatedAt)
    {
        if (Status is OaAssetStatus.Maintenance or OaAssetStatus.Retired)
            throw new InvalidOperationException("维修中或已报废资产不能转移。");
        if (Status == OaAssetStatus.InUse && responsibleUserId is null)
            throw new InvalidOperationException("在用资产转移不能清空责任人，请先归还再重新领用。");
        ResponsibleUserId = responsibleUserId;
        Location = Clean(location);
        UpdatedAt = updatedAt;
    }

    public void SetStatus(OaAssetStatus status, DateTime updatedAt)
    {
        if (Status == OaAssetStatus.InUse && status != OaAssetStatus.Available)
            throw new InvalidOperationException("在用资产必须先归还，不能直接维修或报废。");
        if (status == OaAssetStatus.Available && Status == OaAssetStatus.InUse) Return(updatedAt);
        else { Status = status; UpdatedAt = updatedAt; }
    }

    private static string Required(string? value, string label) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{label}不能为空。") : value.Trim();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class OaAssetAssignment
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid AssetId { get; private set; }
    public Guid UserId { get; private set; }
    public OaAssetAssignmentStatus Status { get; private set; }
    public DateTime AssignedAt { get; private set; }
    public DateTime? ReturnedAt { get; private set; }

    public OaAssetAssignment(Guid assetId, Guid userId, DateTime assignedAt)
    {
        if (assetId == Guid.Empty) throw new ArgumentException("资产不能为空。", nameof(assetId));
        if (userId == Guid.Empty) throw new ArgumentException("领用员工不能为空。", nameof(userId));
        AssetId = assetId;
        UserId = userId;
        AssignedAt = assignedAt;
        Status = OaAssetAssignmentStatus.Active;
    }

    public void Return(DateTime returnedAt)
    {
        if (Status != OaAssetAssignmentStatus.Active) throw new InvalidOperationException("当前资产领用记录不能归还。");
        Status = OaAssetAssignmentStatus.Returned;
        ReturnedAt = returnedAt;
    }

    public void RestoreForRecoveryState(OaAssetAssignmentStatus status, DateTime? returnedAt)
    {
        Status = status;
        ReturnedAt = returnedAt;
    }
}
