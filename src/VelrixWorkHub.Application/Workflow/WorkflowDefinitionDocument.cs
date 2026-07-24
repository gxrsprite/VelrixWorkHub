using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Workflow;

/// <summary>
/// Canvas 与服务端之间的稳定 JSON 合同。页面只需编辑文档，不直接依赖领域对象的内部集合。
/// </summary>
public sealed record WorkflowDefinitionDocument(
    string Code,
    string Name,
    string Description,
    int VersionNumber,
    WorkflowDefinitionStatus Status,
    DateTime CreatedAt,
    DateTime? PublishedAt,
    IReadOnlyList<WorkflowNodeDocument> Nodes,
    IReadOnlyList<WorkflowConnectionDocument> Connections)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = JsonSerializationDefaults.CreateWeb();
        options.WriteIndented = true;
        options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        return options;
    }

    public static WorkflowDefinitionDocument FromDomain(WorkflowDefinition definition) => new(
        definition.Code,
        definition.Name,
        definition.Description,
        definition.VersionNumber,
        definition.Status,
        definition.CreatedAt,
        definition.PublishedAt,
        definition.Nodes.Select(WorkflowNodeDocument.FromDomain).ToArray(),
        definition.Connections.Select(WorkflowConnectionDocument.FromDomain).ToArray());

    public WorkflowDefinition ToDomain()
    {
        if (Nodes is null || Connections is null) throw new InvalidOperationException("Workflow JSON 缺少节点或连线集合。");
        var definition = new WorkflowDefinition(Code, Name, VersionNumber, Description, CreatedAt);
        foreach (var node in Nodes) definition.AddNode(node.Id, node.Type, node.Name, node.X, node.Y, node.ConfigJson);
        foreach (var connection in Connections) definition.Connect(connection.SourceNodeId, connection.TargetNodeId, connection.ConditionKey);
        if (Status is WorkflowDefinitionStatus.Published or WorkflowDefinitionStatus.Archived)
        {
            definition.Publish(PublishedAt);
            if (Status == WorkflowDefinitionStatus.Archived) definition.Archive();
        }
        return definition;
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static WorkflowDefinitionDocument FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Workflow JSON 不能为空。", nameof(json));
        return JsonSerializer.Deserialize<WorkflowDefinitionDocument>(json, JsonOptions)
            ?? throw new ArgumentException("Workflow JSON 不能解析为空文档。", nameof(json));
    }
}

public sealed record WorkflowNodeDocument(Guid Id, WorkflowNodeType Type, string Name, double X, double Y, string ConfigJson)
{
    public static WorkflowNodeDocument FromDomain(WorkflowNode node) => new(node.Id, node.Type, node.Name, node.X, node.Y, node.ConfigJson);
}

public sealed record WorkflowConnectionDocument(Guid SourceNodeId, Guid TargetNodeId, string? ConditionKey)
{
    public static WorkflowConnectionDocument FromDomain(WorkflowConnection connection) => new(connection.SourceNodeId, connection.TargetNodeId, connection.ConditionKey);
}
