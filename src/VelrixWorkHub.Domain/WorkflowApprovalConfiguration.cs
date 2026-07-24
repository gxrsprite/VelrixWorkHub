using System.Text.Json;

namespace VelrixWorkHub.Domain;

/// <summary>同一审批节点存在多个审批人时的完成策略。</summary>
public enum WorkflowApprovalMode
{
    All,
    Any,
    Majority,
    Quorum
}

public static class WorkflowApprovalConfiguration
{
    public static WorkflowApprovalMode ParseMode(string configJson)
    {
        using var document = JsonDocument.Parse(configJson);
        if (!document.RootElement.TryGetProperty("approvalMode", out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return WorkflowApprovalMode.All;
        if (value.ValueKind != JsonValueKind.String || !Enum.TryParse<WorkflowApprovalMode>(value.GetString(), ignoreCase: true, out var mode))
            throw new ArgumentException("approvalMode 只能是 All、Any、Majority 或 Quorum。", nameof(configJson));
        if (mode == WorkflowApprovalMode.Quorum) _ = ParseRequiredApprovals(document.RootElement, configJson);
        return mode;
    }

    public static int GetRequiredApprovals(string configJson)
    {
        using var document = JsonDocument.Parse(configJson);
        return ParseRequiredApprovals(document.RootElement, configJson);
    }

    private static int ParseRequiredApprovals(JsonElement root, string configJson)
    {
        if (!root.TryGetProperty("requiredApprovals", out var value) || !value.TryGetInt32(out var required) || required < 1)
            throw new ArgumentException("approvalMode 为 Quorum 时 requiredApprovals 必须是大于 0 的整数。", nameof(configJson));
        return required;
    }
}
