using System.Text.Json;

namespace VelrixWorkHub.Domain;

public enum WorkflowDefinitionStatus { Draft, Published, Archived }
public enum WorkflowNodeType { Start, Condition, Approval, Notification, BusinessAction, End, ParallelSplit, ParallelJoin, Loop }

public sealed class WorkflowDefinition
{
    private readonly List<WorkflowNode> nodes = [];
    private readonly List<WorkflowConnection> connections = [];

    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string Code { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public int VersionNumber { get; init; }
    public WorkflowDefinitionStatus Status { get; private set; } = WorkflowDefinitionStatus.Draft;
    public DateTime CreatedAt { get; init; }
    public DateTime? PublishedAt { get; private set; }
    public IReadOnlyList<WorkflowNode> Nodes => nodes;
    public IReadOnlyList<WorkflowConnection> Connections => connections;

    public WorkflowDefinition(string code, string name, int versionNumber = 1, string? description = null, DateTime? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("流程编码不能为空。", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("流程名称不能为空。", nameof(name));
        if (versionNumber < 1) throw new ArgumentOutOfRangeException(nameof(versionNumber));
        // 流程编码是稳定标识而非展示文本；统一规范化后，PostgreSQL 的大小写敏感
        // 索引与 SQL Server 的默认比较规则不会产生两套运行实例。
        Code = code.Trim().ToUpperInvariant(); Name = name.Trim(); Description = description?.Trim() ?? string.Empty; VersionNumber = versionNumber; CreatedAt = createdAt ?? DateTime.Now;
    }

    public WorkflowNode AddNode(Guid id, WorkflowNodeType type, string name, double x = 0, double y = 0, string? configJson = null)
    {
        EnsureDraft();
        if (id == Guid.Empty) throw new ArgumentException("节点 ID 不能为空。", nameof(id));
        if (nodes.Any(x => x.Id == id)) throw new InvalidOperationException("流程节点 ID 不能重复。");
        var node = new WorkflowNode(id, type, name, x, y, configJson);
        nodes.Add(node);
        return node;
    }

    public void Connect(Guid sourceId, Guid targetId, string? conditionKey = null)
    {
        EnsureDraft();
        if (sourceId == Guid.Empty || targetId == Guid.Empty || sourceId == targetId) throw new ArgumentException("流程连线的起点和终点必须是不同的有效节点。");
        if (connections.Any(x => x.SourceNodeId == sourceId && x.TargetNodeId == targetId && x.ConditionKey == conditionKey)) throw new InvalidOperationException("流程连线不能重复。");
        connections.Add(new WorkflowConnection(sourceId, targetId, conditionKey));
    }

    public WorkflowValidationResult Validate() => WorkflowDefinitionValidator.Validate(this);

    public void Publish(DateTime? publishedAt = null)
    {
        EnsureDraft();
        var result = Validate();
        if (!result.IsValid) throw new InvalidOperationException($"流程发布校验失败：{string.Join("；", result.Errors)}");
        Status = WorkflowDefinitionStatus.Published;
        PublishedAt = publishedAt ?? DateTime.Now;
    }

    public void Archive()
    {
        if (Status != WorkflowDefinitionStatus.Published) throw new InvalidOperationException("只有已发布流程可以归档。");
        Status = WorkflowDefinitionStatus.Archived;
    }

    private void EnsureDraft()
    {
        if (Status != WorkflowDefinitionStatus.Draft) throw new InvalidOperationException("已发布或已归档流程不可修改，请创建新版本。");
    }
}

public sealed class WorkflowNode
{
    public Guid Id { get; }
    public WorkflowNodeType Type { get; }
    public string Name { get; }
    public double X { get; }
    public double Y { get; }
    public string ConfigJson { get; }

    internal WorkflowNode(Guid id, WorkflowNodeType type, string name, double x, double y, string? configJson)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("节点名称不能为空。", nameof(name));
        if (!double.IsFinite(x) || !double.IsFinite(y)) throw new ArgumentException("节点坐标必须是有限数值。");
        Id = id; Type = type; Name = name.Trim(); X = x; Y = y; ConfigJson = configJson?.Trim() ?? "{}";
        ValidateJson(ConfigJson);
    }

    private static void ValidateJson(string configJson)
    {
        try { using var _ = JsonDocument.Parse(configJson); }
        catch (JsonException ex) { throw new ArgumentException("节点配置必须是有效 JSON。", nameof(configJson), ex); }
    }
}

public sealed record WorkflowConnection(Guid SourceNodeId, Guid TargetNodeId, string? ConditionKey = null);
