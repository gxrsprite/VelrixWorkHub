using System.Text.Json;

namespace VelrixWorkHub.Domain;

/// <summary>
/// Notification 节点的声明式配置。接收人由流程定义固定，投递仍通过统一通知服务完成。
/// </summary>
public sealed record WorkflowNotificationDefinition
{
    public IReadOnlyList<string> Recipients { get; init; } = [];
    public string? Title { get; init; }
    public string? Content { get; init; }
    public string? Href { get; init; }
    public WorkNotificationKind Kind { get; init; } = WorkNotificationKind.System;

    public static WorkflowNotificationDefinition Parse(string configJson, string defaultTitle, string defaultContent)
    {
        using var document = JsonDocument.Parse(configJson);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new ArgumentException("通知节点配置必须是 JSON 对象。", nameof(configJson));
        var recipients = new List<string>();
        if (root.TryGetProperty("recipients", out var value))
        {
            if (value.ValueKind == JsonValueKind.String) recipients.Add(value.GetString() ?? string.Empty);
            else if (value.ValueKind == JsonValueKind.Array)
                recipients.AddRange(value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString() ?? string.Empty));
        }

        var kind = WorkNotificationKind.System;
        if (root.TryGetProperty("kind", out var kindElement) && kindElement.ValueKind == JsonValueKind.String &&
            !Enum.TryParse(kindElement.GetString(), ignoreCase: true, out kind))
            throw new ArgumentException("通知节点的 kind 无效。", nameof(configJson));

        var result = new WorkflowNotificationDefinition
        {
            Recipients = recipients.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Title = ReadText(root, "title") ?? defaultTitle,
            Content = ReadText(root, "content") ?? defaultContent,
            Href = ReadText(root, "href"),
            Kind = kind
        };
        if (result.Recipients.Count == 0) throw new ArgumentException("通知节点至少需要一个接收人。", nameof(configJson));
        if (string.IsNullOrWhiteSpace(result.Title) || string.IsNullOrWhiteSpace(result.Content)) throw new ArgumentException("通知节点标题和内容不能为空。", nameof(configJson));
        return result;
    }

    private static string? ReadText(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : null;
}
