using System.Text.Json;

namespace VelrixWorkHub.Domain;

/// <summary>审批节点的回退目标配置。JSON：{ "returnTargets": ["节点 Guid"] }。</summary>
public static class WorkflowReturnConfiguration
{
    public static IReadOnlySet<Guid> ParseTargets(string configJson)
    {
        using var document = JsonDocument.Parse(configJson);
        if (!document.RootElement.TryGetProperty("returnTargets", out var targets) || targets.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return new HashSet<Guid>();
        if (targets.ValueKind != JsonValueKind.Array) throw new ArgumentException("returnTargets 必须是节点标识数组。", nameof(configJson));

        var result = new HashSet<Guid>();
        foreach (var target in targets.EnumerateArray())
        {
            if (target.ValueKind != JsonValueKind.String || !Guid.TryParse(target.GetString(), out var nodeId) || nodeId == Guid.Empty)
                throw new ArgumentException("returnTargets 必须包含有效的节点标识。", nameof(configJson));
            if (!result.Add(nodeId)) throw new ArgumentException("returnTargets 不能包含重复节点。", nameof(configJson));
        }
        return result;
    }
}
