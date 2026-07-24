namespace VelrixWorkHub.Domain;

public enum OaConsumableTransactionKind
{
    Inbound,
    Issued
}

/// <summary>行政消耗品目录，不映射 ERP 商品、仓库或采购收货。</summary>
public sealed class OaConsumableSupply
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Unit { get; private set; } = string.Empty;
    public string? Location { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string OtherInfo { get; private set; } = "{}";
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public OaConsumableSupply(string code, string name, string unit, string? location, string? otherInfo, DateTime createdAt)
    {
        CreatedAt = createdAt;
        Edit(code, name, unit, location, otherInfo, createdAt);
    }

    public void Edit(string code, string name, string unit, string? location, string? otherInfo, DateTime updatedAt)
    {
        Code = Required(code, "物品编码");
        Name = Required(name, "物品名称");
        Unit = Required(unit, "计量单位");
        Location = Clean(location);
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
        UpdatedAt = updatedAt;
    }

    public void SetActive(bool isActive, DateTime updatedAt) { IsActive = isActive; UpdatedAt = updatedAt; }
    private static string Required(string? value, string label) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{label}不能为空。") : value.Trim();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>行政消耗品入库/发放流水；数量恒为正，由类型决定正负方向。</summary>
public sealed class OaConsumableTransaction
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid SupplyId { get; private set; }
    public OaConsumableTransactionKind Kind { get; private set; }
    public decimal Quantity { get; private set; }
    public Guid? RecipientUserId { get; private set; }
    public string SourceNo { get; private set; } = string.Empty;
    public string ActorName { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public decimal SignedQuantity => Kind == OaConsumableTransactionKind.Inbound ? Quantity : -Quantity;

    public OaConsumableTransaction(Guid supplyId, OaConsumableTransactionKind kind, decimal quantity, Guid? recipientUserId,
        string sourceNo, string actorName, string? notes, DateTime occurredAt)
    {
        if (supplyId == Guid.Empty) throw new ArgumentException("办公用品不能为空。", nameof(supplyId));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "数量必须大于零。");
        if (kind == OaConsumableTransactionKind.Issued && recipientUserId is null) throw new ArgumentException("发放办公用品必须指定接收员工。", nameof(recipientUserId));
        if (kind == OaConsumableTransactionKind.Inbound && recipientUserId is not null) throw new ArgumentException("入库办公用品不能指定接收员工。", nameof(recipientUserId));
        if (string.IsNullOrWhiteSpace(sourceNo)) throw new ArgumentException("来源单号不能为空。", nameof(sourceNo));
        if (string.IsNullOrWhiteSpace(actorName)) throw new ArgumentException("操作者不能为空。", nameof(actorName));
        SupplyId = supplyId; Kind = kind; Quantity = decimal.Round(quantity, 4); RecipientUserId = recipientUserId;
        SourceNo = sourceNo.Trim(); ActorName = actorName.Trim(); Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(); OccurredAt = occurredAt;
    }
}
