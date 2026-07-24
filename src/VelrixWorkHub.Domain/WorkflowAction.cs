using System.Text.Json;

namespace VelrixWorkHub.Domain;

public enum WorkflowActionTrigger { Approved, Rejected, Cancelled }
public enum WorkflowActionType { SetField }

/// <summary>
/// 流程节点上的声明式业务动作。字段和值使用业务模块注册的白名单，不能直接执行任意代码。
/// </summary>
public sealed record WorkflowActionDefinition
{
    public WorkflowActionType Type { get; init; }
    public string Field { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;

    public WorkflowActionDefinition() { }

    public WorkflowActionDefinition(WorkflowActionType type, string field, string value)
    {
        Type = type;
        Field = field?.Trim() ?? string.Empty;
        Value = value?.Trim() ?? string.Empty;
        Validate();
    }

    public void Validate()
    {
        if (!Enum.IsDefined(Type)) throw new ArgumentOutOfRangeException(nameof(Type));
        if (string.IsNullOrWhiteSpace(Field)) throw new ArgumentException("流程动作字段不能为空。", nameof(Field));
        if (string.IsNullOrWhiteSpace(Value)) throw new ArgumentException("流程动作值不能为空。", nameof(Value));
    }
}

/// <summary>
/// Approval 节点的动作配置。配置位于节点 JSON 中，会随流程实例快照固定下来。
/// </summary>
public sealed class WorkflowNodeActionConfiguration
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializationDefaults.CreateWeb();

    public WorkflowActionDefinition? OnApproved { get; init; }
    public WorkflowActionDefinition? OnRejected { get; init; }
    public WorkflowActionDefinition? OnCancelled { get; init; }
    public WorkflowActionDefinition? Action { get; init; }

    public WorkflowActionDefinition? Get(WorkflowActionTrigger trigger) => trigger switch
    {
        WorkflowActionTrigger.Approved => OnApproved,
        WorkflowActionTrigger.Rejected => OnRejected,
        WorkflowActionTrigger.Cancelled => OnCancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(trigger))
    };

    public static WorkflowNodeActionConfiguration Parse(string configJson)
    {
        var configuration = JsonSerializer.Deserialize<WorkflowNodeActionConfiguration>(configJson, JsonOptions) ?? new();
        configuration.OnApproved?.Validate();
        configuration.OnRejected?.Validate();
        configuration.OnCancelled?.Validate();
        configuration.Action?.Validate();
        return configuration;
    }

    public static WorkflowActionDefinition? ParseNodeAction(string configJson)
        => Parse(configJson).Action;
}
