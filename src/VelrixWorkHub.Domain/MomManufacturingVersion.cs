namespace VelrixWorkHub.Domain;

public enum MomManufacturingVersionStatus { Draft, Released, Retired }

/// <summary>
/// MOM 对 ERP/PLM 产品结构的制造版本引用。MOM 保存执行所需的版本边界，不复制 PLM 图文档。
/// </summary>
public sealed class MomManufacturingVersion
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid ProductId { get; private set; }
    public string VersionCode { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public string? EngineeringChangeReference { get; private set; }
    public MomManufacturingVersionStatus Status { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomManufacturingVersion(Guid productId, string versionCode, string name, DateOnly effectiveFrom,
        DateOnly? effectiveTo = null, string? engineeringChangeReference = null, string? otherInfo = null)
    {
        Edit(productId, versionCode, name, effectiveFrom, effectiveTo, engineeringChangeReference, otherInfo);
        Status = MomManufacturingVersionStatus.Draft;
    }

    public static MomManufacturingVersion Restore(Guid id, Guid productId, string versionCode, string name,
        DateOnly effectiveFrom, DateOnly? effectiveTo, string? engineeringChangeReference,
        MomManufacturingVersionStatus status, string? otherInfo)
    {
        var item = new MomManufacturingVersion(productId, versionCode, name, effectiveFrom, effectiveTo, engineeringChangeReference, otherInfo)
        {
            Id = id,
            Status = status
        };
        return item;
    }

    public void Edit(Guid productId, string versionCode, string name, DateOnly effectiveFrom,
        DateOnly? effectiveTo = null, string? engineeringChangeReference = null, string? otherInfo = null)
    {
        if (Status != MomManufacturingVersionStatus.Draft) throw new InvalidOperationException("只有草稿制造版本可以编辑。");
        if (productId == Guid.Empty) throw new ArgumentException("制造版本必须绑定产品。", nameof(productId));
        if (string.IsNullOrWhiteSpace(versionCode)) throw new ArgumentException("制造版本编码不能为空。", nameof(versionCode));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("制造版本名称不能为空。", nameof(name));
        if (effectiveTo is DateOnly end && end < effectiveFrom) throw new ArgumentException("制造版本有效期结束日不能早于开始日。", nameof(effectiveTo));
        ProductId = productId;
        VersionCode = versionCode.Trim();
        Name = name.Trim();
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        EngineeringChangeReference = Clean(engineeringChangeReference);
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public void Release()
    {
        if (Status != MomManufacturingVersionStatus.Draft) throw new InvalidOperationException("只有草稿制造版本可以发布。");
        Status = MomManufacturingVersionStatus.Released;
    }

    public void Retire()
    {
        if (Status != MomManufacturingVersionStatus.Released) throw new InvalidOperationException("只有已发布制造版本可以停用。");
        Status = MomManufacturingVersionStatus.Retired;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// 制造版本的物料组件。ComponentProductId 只保存稳定商品引用，实际产品主数据仍由 ERP 负责。
/// </summary>
public sealed class MomManufacturingComponent
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid ManufacturingVersionId { get; private set; }
    public int LineNo { get; private set; }
    public Guid ComponentProductId { get; private set; }
    public decimal QuantityPer { get; private set; }
    public decimal ScrapRatePercent { get; private set; }
    public int OperationSequence { get; private set; }
    public string? Notes { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomManufacturingComponent(Guid manufacturingVersionId, int lineNo, Guid componentProductId,
        decimal quantityPer, decimal scrapRatePercent = 0, int operationSequence = 10,
        string? notes = null, string? otherInfo = null)
    {
        Edit(manufacturingVersionId, lineNo, componentProductId, quantityPer, scrapRatePercent, operationSequence, notes, otherInfo);
    }

    public static MomManufacturingComponent Restore(Guid id, Guid manufacturingVersionId, int lineNo, Guid componentProductId,
        decimal quantityPer, decimal scrapRatePercent, int operationSequence, string? notes, string? otherInfo)
    {
        var item = new MomManufacturingComponent(manufacturingVersionId, lineNo, componentProductId, quantityPer, scrapRatePercent, operationSequence, notes, otherInfo) { Id = id };
        return item;
    }

    public void Edit(Guid manufacturingVersionId, int lineNo, Guid componentProductId, decimal quantityPer,
        decimal scrapRatePercent = 0, int operationSequence = 10, string? notes = null, string? otherInfo = null)
    {
        if (manufacturingVersionId == Guid.Empty) throw new ArgumentException("组件必须绑定制造版本。", nameof(manufacturingVersionId));
        if (lineNo <= 0) throw new ArgumentOutOfRangeException(nameof(lineNo), "组件行号必须大于 0。");
        if (componentProductId == Guid.Empty) throw new ArgumentException("组件商品不能为空。", nameof(componentProductId));
        if (quantityPer <= 0) throw new ArgumentOutOfRangeException(nameof(quantityPer), "单位用量必须大于 0。");
        if (scrapRatePercent < 0 || scrapRatePercent > 100) throw new ArgumentOutOfRangeException(nameof(scrapRatePercent), "损耗率必须在 0 到 100 之间。");
        if (operationSequence < 0) throw new ArgumentOutOfRangeException(nameof(operationSequence), "工序顺序不能为负数。");
        ManufacturingVersionId = manufacturingVersionId;
        LineNo = lineNo;
        ComponentProductId = componentProductId;
        QuantityPer = quantityPer;
        ScrapRatePercent = scrapRatePercent;
        OperationSequence = operationSequence;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }
}
