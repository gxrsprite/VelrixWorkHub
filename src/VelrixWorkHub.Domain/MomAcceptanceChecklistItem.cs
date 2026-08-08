namespace VelrixWorkHub.Domain;

public enum MomAcceptanceItemResult { Pending, Passed, Failed, NotApplicable }

/// <summary>FAT/SAT 验收检查项。检查项在验收单草稿阶段维护，提交后只读。</summary>
public sealed class MomAcceptanceChecklistItem
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid AcceptanceId { get; private set; }
    public int LineNo { get; private set; }
    public string ItemCode { get; private set; } = string.Empty;
    public string ItemName { get; private set; } = string.Empty;
    public string Requirement { get; private set; } = string.Empty;
    public MomAcceptanceItemResult Result { get; private set; }
    public string? Remark { get; private set; }
    public string? CheckedBy { get; private set; }
    public DateTime? CheckedOn { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomAcceptanceChecklistItem(Guid acceptanceId, int lineNo, string itemCode, string itemName, string requirement,
        string? otherInfo = null, Guid? id = null)
    {
        if (acceptanceId == Guid.Empty) throw new ArgumentException("验收单不能为空。", nameof(acceptanceId));
        if (lineNo <= 0) throw new ArgumentOutOfRangeException(nameof(lineNo), "检查项行号必须大于零。");
        AcceptanceId = acceptanceId; LineNo = lineNo; ItemCode = Normalize(itemCode, 80, "检查项编码");
        ItemName = Normalize(itemName, 200, "检查项名称"); Requirement = Normalize(requirement, 1000, "检查要求");
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo)); Id = id ?? Guid.CreateVersion7(); Result = MomAcceptanceItemResult.Pending;
    }

    public static MomAcceptanceChecklistItem Restore(Guid id, Guid acceptanceId, int lineNo, string itemCode, string itemName,
        string requirement, MomAcceptanceItemResult result, string? remark, string? checkedBy, DateTime? checkedOn, string? otherInfo)
    {
        var item = new MomAcceptanceChecklistItem(acceptanceId, lineNo, itemCode, itemName, requirement, otherInfo, id);
        item.Result = result; item.Remark = Clean(remark); item.CheckedBy = Clean(checkedBy); item.CheckedOn = checkedOn;
        return item;
    }

    public void SetResult(MomAcceptanceItemResult result, string? remark, string actor, DateTime checkedOn)
    {
        if (!Enum.IsDefined(result)) throw new ArgumentOutOfRangeException(nameof(result), "检查项结果无效。");
        if (result == MomAcceptanceItemResult.Pending) throw new InvalidOperationException("检查项必须选择通过、不通过或不适用。");
        Result = result; Remark = NormalizeOptional(remark, 1000, "检查项备注"); CheckedBy = Normalize(actor, 100, "检查人"); CheckedOn = checkedOn;
    }

    public void RestoreResult(MomAcceptanceItemResult result, string? remark, string? checkedBy, DateTime? checkedOn)
    {
        Result = result; Remark = Clean(remark); CheckedBy = Clean(checkedBy); CheckedOn = checkedOn;
    }

    private static string Normalize(string? value, int maxLength, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{label}不能为空。", nameof(value));
        var result = value.Trim();
        if (result.Length > maxLength) throw new ArgumentException($"{label}最多 {maxLength} 个字符。", nameof(value));
        return result;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var result = value.Trim();
        if (result.Length > maxLength) throw new ArgumentException($"{label}最多 {maxLength} 个字符。", nameof(value));
        return result;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
