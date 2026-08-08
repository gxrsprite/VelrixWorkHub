namespace VelrixWorkHub.Domain;

public enum MomQualityInspectionStandardStatus { Draft, Active, Inactive }

/// <summary>可配置的检验标准主数据；只有已发布标准可以冻结到质量检验记录。</summary>
public sealed class MomQualityInspectionStandard
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid? ProductId { get; private set; }
    public MomQualityInspectionType InspectionType { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Version { get; private set; } = string.Empty;
    public MomQualityInspectionStandardStatus Status { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomQualityInspectionStandard(Guid? productId, MomQualityInspectionType inspectionType,
        string code, string name, string version, string? otherInfo = null, Guid? id = null)
    {
        Validate(productId, inspectionType, code, name, version);
        Id = id ?? Guid.CreateVersion7(); ProductId = productId; InspectionType = inspectionType;
        Code = code.Trim(); Name = name.Trim(); Version = version.Trim(); OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
        Status = MomQualityInspectionStandardStatus.Draft;
    }

    public static MomQualityInspectionStandard Restore(Guid id, Guid? productId, MomQualityInspectionType inspectionType,
        string code, string name, string version, MomQualityInspectionStandardStatus status, string? otherInfo)
    {
        var item = new MomQualityInspectionStandard(productId, inspectionType, code, name, version, otherInfo, id);
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status), "质量标准状态无效。");
        item.Status = status;
        return item;
    }

    public void Edit(Guid? productId, MomQualityInspectionType inspectionType, string code, string name, string version, string? otherInfo = null)
    {
        if (Status != MomQualityInspectionStandardStatus.Draft) throw new InvalidOperationException("只有草稿质量标准可以编辑。");
        Validate(productId, inspectionType, code, name, version);
        ProductId = productId; InspectionType = inspectionType; Code = code.Trim(); Name = name.Trim(); Version = version.Trim(); OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public void Publish()
    {
        if (Status != MomQualityInspectionStandardStatus.Draft) throw new InvalidOperationException("只有草稿质量标准可以发布。");
        Status = MomQualityInspectionStandardStatus.Active;
    }

    public void Retire()
    {
        if (Status != MomQualityInspectionStandardStatus.Active) throw new InvalidOperationException("只有启用质量标准可以停用。");
        Status = MomQualityInspectionStandardStatus.Inactive;
    }

    public void RestoreStatus(MomQualityInspectionStandardStatus status)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status), "质量标准状态无效。");
        Status = status;
    }

    private static void Validate(Guid? productId, MomQualityInspectionType inspectionType, string code, string name, string version)
    {
        if (productId == Guid.Empty) throw new ArgumentException("质量标准商品引用无效。", nameof(productId));
        if (!Enum.IsDefined(inspectionType)) throw new ArgumentOutOfRangeException(nameof(inspectionType), "质量检验类型无效。");
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("质量标准编码不能为空。", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("质量标准名称不能为空。", nameof(name));
        if (string.IsNullOrWhiteSpace(version)) throw new ArgumentException("质量标准版本不能为空。", nameof(version));
    }
}

/// <summary>质量标准中的单个检验项目，数值上下限为空时表示定性要求。</summary>
public sealed class MomQualityInspectionStandardItem
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid StandardId { get; private set; }
    public int LineNo { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Requirement { get; private set; } = string.Empty;
    public string? Unit { get; private set; }
    public decimal? MinValue { get; private set; }
    public decimal? MaxValue { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomQualityInspectionStandardItem(Guid standardId, int lineNo, string code, string name, string requirement,
        string? unit, decimal? minValue, decimal? maxValue, string? otherInfo = null, Guid? id = null)
    {
        Validate(standardId, lineNo, code, name, requirement, minValue, maxValue);
        Id = id ?? Guid.CreateVersion7(); StandardId = standardId; LineNo = lineNo; Code = code.Trim(); Name = name.Trim(); Requirement = requirement.Trim();
        Unit = Clean(unit); MinValue = minValue; MaxValue = maxValue; OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public static MomQualityInspectionStandardItem Restore(Guid id, Guid standardId, int lineNo, string code, string name,
        string requirement, string? unit, decimal? minValue, decimal? maxValue, string? otherInfo)
        => new(standardId, lineNo, code, name, requirement, unit, minValue, maxValue, otherInfo, id);

    public void Edit(int lineNo, string code, string name, string requirement, string? unit, decimal? minValue, decimal? maxValue, string? otherInfo = null)
    {
        Validate(StandardId, lineNo, code, name, requirement, minValue, maxValue);
        LineNo = lineNo; Code = code.Trim(); Name = name.Trim(); Requirement = requirement.Trim(); Unit = Clean(unit); MinValue = minValue; MaxValue = maxValue; OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public void RestoreForRecovery(int lineNo, string code, string name, string requirement, string? unit, decimal? minValue, decimal? maxValue, string? otherInfo)
    {
        Validate(StandardId, lineNo, code, name, requirement, minValue, maxValue);
        LineNo = lineNo; Code = code.Trim(); Name = name.Trim(); Requirement = requirement.Trim(); Unit = Clean(unit); MinValue = minValue; MaxValue = maxValue; OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    private static void Validate(Guid standardId, int lineNo, string code, string name, string requirement, decimal? minValue, decimal? maxValue)
    {
        if (standardId == Guid.Empty) throw new ArgumentException("检验项目必须绑定质量标准。", nameof(standardId));
        if (lineNo <= 0) throw new ArgumentOutOfRangeException(nameof(lineNo), "检验项目行号必须大于零。");
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("检验项目编码不能为空。", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("检验项目名称不能为空。", nameof(name));
        if (string.IsNullOrWhiteSpace(requirement)) throw new ArgumentException("检验项目要求不能为空。", nameof(requirement));
        if (minValue is decimal min && maxValue is decimal max && min > max) throw new InvalidOperationException("检验项目最小值不能大于最大值。");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record MomQualityStandardItemSnapshot(int LineNo, string Code, string Name, string Requirement, string? Unit, decimal? MinValue, decimal? MaxValue);
public sealed record MomQualityStandardSnapshot(Guid StandardId, Guid? ProductId, MomQualityInspectionType InspectionType, string Code, string Name, string Version, IReadOnlyList<MomQualityStandardItemSnapshot> Items);
