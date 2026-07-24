using System.Text.Json;

namespace VelrixWorkHub.Domain;

/// <summary>显式循环的上限配置；连线固定使用 repeat 与 exit 两个分支键。</summary>
public sealed record WorkflowLoopConfiguration(int MaxIterations)
{
    public const string RepeatKey = "repeat";
    public const string ExitKey = "exit";

    public static WorkflowLoopConfiguration Parse(string configJson)
    {
        using var document = JsonDocument.Parse(configJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("maxIterations", out var value)
            || !value.TryGetInt32(out var maxIterations))
            throw new ArgumentException("缺少整数配置“maxIterations”。", nameof(configJson));
        if (maxIterations is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(configJson), "maxIterations 必须在 1 到 100 之间。");
        return new WorkflowLoopConfiguration(maxIterations);
    }
}
