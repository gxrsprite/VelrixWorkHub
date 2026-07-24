using System.Text.Json;

namespace VelrixWorkHub.Domain;

public enum WorkflowInstanceStatus { Running, Completed, Rejected, Cancelled }

/// <summary>
/// 运行态实例只引用已发布定义，并保存启动时的不可变图快照。
/// </summary>
public sealed class WorkflowInstance
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializationDefaults.CreateWeb();

    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid DefinitionId { get; }
    public string DefinitionCode { get; }
    public int DefinitionVersion { get; }
    public string BusinessType { get; }
    public Guid BusinessId { get; }
    public string StartedBy { get; }
    public Guid? PreviousInstanceId { get; }
    public string DefinitionSnapshotJson { get; }
    public WorkflowInstanceStatus Status { get; private set; } = WorkflowInstanceStatus.Running;
    public Guid CurrentNodeId { get; private set; }
    /// <summary>
    /// 当前激活节点集合。线性流程始终只包含 CurrentNodeId；并行流程可同时包含多个分支节点。
    /// JSON 持久化后与定义快照一同保持跨进程运行态一致。
    /// </summary>
    public IReadOnlySet<Guid> ActiveNodeIds => activeNodeIds;
    public string ActiveNodeIdsJson => JsonSerializer.Serialize(activeNodeIds.OrderBy(x => x), JsonOptions);
    /// <summary>并行汇聚节点已到达的来源分支，未全部到达前不激活 Join 节点。</summary>
    public string ParallelJoinArrivalsJson => JsonSerializer.Serialize(parallelJoinArrivals.ToDictionary(x => x.Key, x => x.Value.OrderBy(y => y).ToArray()), JsonOptions);
    /// <summary>每个显式 Loop 节点已经执行的次数；用于跨进程恢复受控循环上限。</summary>
    public string LoopIterationsJson => JsonSerializer.Serialize(loopIterations.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value), JsonOptions);
    /// <summary>审批节点首次进入时解析的审批人快照；用于重启后的幂等补偿，不能随组织成员变更扩容。</summary>
    public string ApprovalAssigneesJson => JsonSerializer.Serialize(approvalAssignees.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value), JsonOptions);
    /// <summary>
    /// 持久化版本号，用于仓储层乐观并发控制。新实例从 1 开始。
    /// </summary>
    public long Revision { get; private set; } = 1;
    public DateTime StartedAt { get; }
    public DateTime? CompletedAt { get; private set; }
    private readonly IReadOnlyDictionary<Guid, string> snapshotNodeConfigs;
    private readonly IReadOnlyDictionary<Guid, WorkflowNodeType> snapshotNodeTypes;
    private readonly IReadOnlyDictionary<Guid, string> snapshotNodeNames;
    private readonly IReadOnlyList<WorkflowConnection> snapshotConnections;
    private readonly HashSet<Guid> activeNodeIds;
    private readonly Dictionary<Guid, HashSet<Guid>> parallelJoinArrivals;
    private readonly Dictionary<Guid, int> loopIterations;
    private readonly Dictionary<Guid, string[]> approvalAssignees;

    private WorkflowInstance(WorkflowDefinition definition, string businessType, Guid businessId, DateTime startedAt, string startedBy, Guid? previousInstanceId)
    {
        DefinitionId = definition.Id;
        DefinitionCode = definition.Code;
        DefinitionVersion = definition.VersionNumber;
        BusinessType = businessType.Trim();
        BusinessId = businessId;
        StartedBy = startedBy.Trim();
        PreviousInstanceId = previousInstanceId;
        StartedAt = startedAt;
        CurrentNodeId = definition.Nodes.Single(x => x.Type == WorkflowNodeType.Start).Id;
        DefinitionSnapshotJson = JsonSerializer.Serialize(new
        {
            definition.Id,
            definition.Code,
            definition.Name,
            definition.VersionNumber,
            Nodes = definition.Nodes.Select(x => new { x.Id, x.Type, x.Name, x.X, x.Y, x.ConfigJson }),
            Connections = definition.Connections
        }, JsonOptions);
        (snapshotNodeConfigs, snapshotNodeTypes, snapshotNodeNames, snapshotConnections) = ParseSnapshot(DefinitionSnapshotJson);
        activeNodeIds = [CurrentNodeId];
        parallelJoinArrivals = [];
        loopIterations = [];
        approvalAssignees = [];
    }

    private WorkflowInstance(Guid id, Guid definitionId, string definitionCode, int definitionVersion, string businessType, Guid businessId, string startedBy, Guid? previousInstanceId, string snapshotJson, WorkflowInstanceStatus status, Guid currentNodeId, DateTime startedAt, DateTime? completedAt, long revision, string? activeNodeIdsJson, string? parallelJoinArrivalsJson, string? loopIterationsJson, string? approvalAssigneesJson)
    {
        Id = id;
        DefinitionId = definitionId;
        DefinitionCode = definitionCode.Trim().ToUpperInvariant();
        DefinitionVersion = definitionVersion;
        BusinessType = businessType;
        BusinessId = businessId;
        StartedBy = startedBy;
        PreviousInstanceId = previousInstanceId;
        DefinitionSnapshotJson = snapshotJson;
        Status = status;
        CurrentNodeId = currentNodeId;
        Revision = revision;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        (snapshotNodeConfigs, snapshotNodeTypes, snapshotNodeNames, snapshotConnections) = ParseSnapshot(snapshotJson);
        activeNodeIds = ParseActiveNodeIds(activeNodeIdsJson, currentNodeId, snapshotNodeTypes);
        parallelJoinArrivals = ParseParallelJoinArrivals(parallelJoinArrivalsJson, activeNodeIds, snapshotNodeConfigs.Keys, snapshotNodeTypes, snapshotConnections);
        loopIterations = ParseLoopIterations(loopIterationsJson, snapshotNodeConfigs.Keys, snapshotNodeTypes, snapshotNodeConfigs);
        approvalAssignees = ParseApprovalAssignees(approvalAssigneesJson, snapshotNodeConfigs.Keys, snapshotNodeTypes);
    }

    public static WorkflowInstance Start(WorkflowDefinition definition, string businessType, Guid businessId, DateTime? startedAt = null, string? startedBy = null, Guid? previousInstanceId = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Status != WorkflowDefinitionStatus.Published) throw new InvalidOperationException("只有已发布流程可以启动实例。");
        if (!definition.Validate().IsValid) throw new InvalidOperationException("流程定义未通过发布校验，不能启动实例。");
        if (string.IsNullOrWhiteSpace(businessType)) throw new ArgumentException("业务对象类型不能为空。", nameof(businessType));
        if (businessId == Guid.Empty) throw new ArgumentException("业务对象不能为空。", nameof(businessId));
        var normalizedStartedBy = string.IsNullOrWhiteSpace(startedBy) ? "system" : startedBy.Trim();
        if (normalizedStartedBy.Length > 200) throw new ArgumentException("流程发起人不能超过 200 个字符。", nameof(startedBy));
        if (previousInstanceId == Guid.Empty) throw new ArgumentException("上一次流程实例标识无效。", nameof(previousInstanceId));
        return new WorkflowInstance(definition, businessType, businessId, startedAt ?? DateTime.Now, normalizedStartedBy, previousInstanceId);
    }

    public static WorkflowInstance Rehydrate(Guid id, Guid definitionId, string definitionCode, int definitionVersion, string businessType, Guid businessId, string? startedBy, string snapshotJson, WorkflowInstanceStatus status, Guid currentNodeId, DateTime startedAt, DateTime? completedAt, Guid? previousInstanceId = null, long revision = 1, string? activeNodeIdsJson = null, string? parallelJoinArrivalsJson = null, string? loopIterationsJson = null, string? approvalAssigneesJson = null)
    {
        if (id == Guid.Empty || definitionId == Guid.Empty || businessId == Guid.Empty || currentNodeId == Guid.Empty) throw new ArgumentException("流程实例标识不能为空。");
        if (string.IsNullOrWhiteSpace(definitionCode) || string.IsNullOrWhiteSpace(businessType) || string.IsNullOrWhiteSpace(snapshotJson)) throw new ArgumentException("流程实例持久化数据不完整。");
        var normalizedStartedBy = string.IsNullOrWhiteSpace(startedBy) ? "system" : startedBy.Trim();
        if (normalizedStartedBy.Length > 200) throw new ArgumentException("流程发起人不能超过 200 个字符。", nameof(startedBy));
        if (previousInstanceId == Guid.Empty) throw new ArgumentException("上一次流程实例标识无效。", nameof(previousInstanceId));
        if (definitionVersion < 1) throw new ArgumentOutOfRangeException(nameof(definitionVersion));
        if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        try { using var _ = JsonDocument.Parse(snapshotJson); } catch (JsonException ex) { throw new ArgumentException("流程实例快照必须是有效 JSON。", nameof(snapshotJson), ex); }
        ValidateSnapshotIdentity(snapshotJson, definitionId, definitionCode, definitionVersion);
        if (status == WorkflowInstanceStatus.Running && completedAt is not null) throw new ArgumentException("运行中的流程实例不能有结束时间。");
        if (status != WorkflowInstanceStatus.Running && completedAt is null) throw new ArgumentException("已结束流程实例必须有结束时间。");
        return new WorkflowInstance(id, definitionId, definitionCode.Trim().ToUpperInvariant(), definitionVersion, businessType.Trim(), businessId, normalizedStartedBy, previousInstanceId, snapshotJson, status, currentNodeId, startedAt, completedAt, revision, activeNodeIdsJson, parallelJoinArrivalsJson, loopIterationsJson, approvalAssigneesJson);
    }

    /// <summary>由持久化仓储在 CAS 成功后推进版本号。</summary>
    public void MarkPersistedRevision(long revision)
    {
        if (revision != Revision + 1) throw new InvalidOperationException("流程实例版本号必须连续递增。");
        Revision = revision;
    }

    /// <summary>CAS 失败时恢复服务层本次未提交的内存变更。</summary>
    public void RestorePersistedState(Guid currentNodeId, WorkflowInstanceStatus status, DateTime? completedAt, long revision, string? activeNodeIdsJson = null, string? parallelJoinArrivalsJson = null, string? loopIterationsJson = null, string? approvalAssigneesJson = null)
    {
        if (currentNodeId == Guid.Empty || revision < 1) throw new ArgumentException("流程实例恢复状态无效。");
        if (!snapshotNodeConfigs.ContainsKey(currentNodeId)) throw new InvalidOperationException("恢复节点不在流程实例快照中。");
        CurrentNodeId = currentNodeId;
        Status = status;
        CompletedAt = completedAt;
        Revision = revision;
        activeNodeIds.Clear();
        activeNodeIds.UnionWith(ParseActiveNodeIds(activeNodeIdsJson, currentNodeId, snapshotNodeTypes));
        parallelJoinArrivals.Clear();
        foreach (var arrival in ParseParallelJoinArrivals(parallelJoinArrivalsJson, activeNodeIds, snapshotNodeConfigs.Keys, snapshotNodeTypes, snapshotConnections))
            parallelJoinArrivals[arrival.Key] = arrival.Value;
        loopIterations.Clear();
        foreach (var iteration in ParseLoopIterations(loopIterationsJson, snapshotNodeConfigs.Keys, snapshotNodeTypes, snapshotNodeConfigs))
            loopIterations[iteration.Key] = iteration.Value;
        if (approvalAssigneesJson is not null)
        {
            approvalAssignees.Clear();
            foreach (var assignees in ParseApprovalAssignees(approvalAssigneesJson, snapshotNodeConfigs.Keys, snapshotNodeTypes))
                approvalAssignees[assignees.Key] = assignees.Value;
        }
    }

    public IReadOnlyList<string> GetApprovalAssignees(Guid nodeId)
        => approvalAssignees.TryGetValue(nodeId, out var assignees) ? assignees : [];

    /// <summary>首次进入审批节点时固化审批人；已有快照不能被后续组织变化覆盖。</summary>
    public void CaptureApprovalAssignees(Guid nodeId, IReadOnlyCollection<string> assignees)
    {
        if (GetNodeType(nodeId) != WorkflowNodeType.Approval) throw new InvalidOperationException("只能为审批节点保存审批人快照。");
        if (approvalAssignees.ContainsKey(nodeId)) return;
        var normalized = NormalizeApprovalAssignees(assignees);
        if (normalized.Length == 0) throw new InvalidOperationException($"审批节点“{GetNodeName(nodeId)}”未解析到可用审批人，流程不能进入无人待办状态。");
        approvalAssignees[nodeId] = normalized;
    }

    public void Complete(DateTime? completedAt = null) => Finish(WorkflowInstanceStatus.Completed, completedAt);
    public void Reject(DateTime? completedAt = null) => Finish(WorkflowInstanceStatus.Rejected, completedAt);
    public void Cancel(DateTime? completedAt = null) => Finish(WorkflowInstanceStatus.Cancelled, completedAt);

    /// <summary>按当前审批节点声明的回退目标回到历史审批节点；回退不是图上的正向连线。</summary>
    public void ReturnTo(Guid sourceNodeId, Guid targetNodeId)
    {
        if (Status != WorkflowInstanceStatus.Running) throw new InvalidOperationException("已结束的流程实例不能回退节点。");
        if (sourceNodeId == Guid.Empty) throw new ArgumentException("来源节点不能为空。", nameof(sourceNodeId));
        if (targetNodeId == Guid.Empty) throw new ArgumentException("目标节点不能为空。", nameof(targetNodeId));
        if (!activeNodeIds.Contains(sourceNodeId) || GetNodeType(sourceNodeId) != WorkflowNodeType.Approval || GetNodeType(targetNodeId) != WorkflowNodeType.Approval)
            throw new InvalidOperationException("只有审批节点可以执行回退。");
        if (!WorkflowReturnConfiguration.ParseTargets(GetNodeConfig(sourceNodeId)).Contains(targetNodeId))
            throw new InvalidOperationException("目标节点未在当前审批节点的 returnTargets 配置中声明。");
        CurrentNodeId = targetNodeId;
        activeNodeIds.Clear();
        activeNodeIds.Add(targetNodeId);
        parallelJoinArrivals.Clear();
    }

    public IReadOnlyList<WorkflowConnection> GetOutgoingTransitions(Guid? sourceNodeId = null)
    {
        var source = sourceNodeId ?? CurrentNodeId;
        if (!snapshotNodeConfigs.ContainsKey(source)) throw new InvalidOperationException("流程实例快照中不存在当前节点。");
        return snapshotConnections.Where(x => x.SourceNodeId == source).ToArray();
    }

    public WorkflowNodeType GetNodeType(Guid nodeId)
        => snapshotNodeTypes.TryGetValue(nodeId, out var type)
            ? type
            : throw new InvalidOperationException("流程实例快照中不存在该节点。");

    public string GetNodeName(Guid nodeId)
        => snapshotNodeNames.TryGetValue(nodeId, out var name)
            ? name
            : throw new InvalidOperationException("流程实例快照中不存在该节点。");

    public string GetNodeConfig(Guid nodeId)
        => snapshotNodeConfigs.TryGetValue(nodeId, out var config)
            ? config
            : throw new InvalidOperationException("流程实例快照中不存在该节点。");

    public void AdvanceTo(Guid targetNodeId, string? conditionKey = null)
    {
        AdvanceActiveNode(CurrentNodeId, targetNodeId, conditionKey);
    }

    /// <summary>推进指定的活动分支；线性流程继续通过 AdvanceTo 调用。</summary>
    public void AdvanceActiveNode(Guid sourceNodeId, Guid targetNodeId, string? conditionKey = null)
    {
        if (Status != WorkflowInstanceStatus.Running) throw new InvalidOperationException("已结束的流程实例不能推进节点。");
        if (!activeNodeIds.Contains(sourceNodeId)) throw new InvalidOperationException("来源节点不是当前活动分支。");
        if (targetNodeId == Guid.Empty) throw new ArgumentException("目标节点不能为空。", nameof(targetNodeId));
        if (!snapshotNodeConfigs.ContainsKey(targetNodeId)) throw new InvalidOperationException("目标节点不在流程实例快照中。");
        if (GetNodeType(targetNodeId) == WorkflowNodeType.ParallelJoin)
            throw new InvalidOperationException("进入 ParallelJoin 必须通过 ArriveAtParallelJoin 记录分支到达，不能按普通节点推进。");
        if (GetNodeType(targetNodeId) == WorkflowNodeType.End && activeNodeIds.Count > 1)
            throw new InvalidOperationException("并行分支不能在其他分支仍活动时直接结束，必须先汇聚到 ParallelJoin。");

        var transition = GetOutgoingTransitions(sourceNodeId).SingleOrDefault(x =>
            x.TargetNodeId == targetNodeId &&
            string.Equals(x.ConditionKey, conditionKey, StringComparison.OrdinalIgnoreCase));
        if (transition is null) throw new InvalidOperationException("目标节点不是当前节点允许推进的连线目标。");
        activeNodeIds.Remove(sourceNodeId);
        activeNodeIds.Add(transition.TargetNodeId);
        CurrentNodeId = transition.TargetNodeId;
    }

    /// <summary>并行拆分节点一次激活所有无条件目标分支。</summary>
    public IReadOnlyList<Guid> SplitParallel(Guid splitNodeId)
    {
        if (Status != WorkflowInstanceStatus.Running || !activeNodeIds.Contains(splitNodeId) || GetNodeType(splitNodeId) != WorkflowNodeType.ParallelSplit)
            throw new InvalidOperationException("当前节点不是活动的并行拆分节点。");
        var targets = GetOutgoingTransitions(splitNodeId).Where(x => x.ConditionKey is null).Select(x => x.TargetNodeId).Distinct().ToArray();
        if (targets.Length < 2) throw new InvalidOperationException("并行拆分节点至少需要两个无条件目标。");
        activeNodeIds.Remove(splitNodeId);
        foreach (var target in targets) activeNodeIds.Add(target);
        CurrentNodeId = targets[0];
        return targets;
    }

    /// <summary>活动分支到达并行汇聚；所有入边来源到达后才激活 Join 节点。</summary>
    public bool ArriveAtParallelJoin(Guid sourceNodeId, Guid joinNodeId)
    {
        if (Status != WorkflowInstanceStatus.Running || !activeNodeIds.Contains(sourceNodeId) || GetNodeType(joinNodeId) != WorkflowNodeType.ParallelJoin)
            throw new InvalidOperationException("并行汇聚到达状态无效。");
        if (!GetOutgoingTransitions(sourceNodeId).Any(x => x.TargetNodeId == joinNodeId))
            throw new InvalidOperationException("并行汇聚节点不是该分支的允许目标。");
        activeNodeIds.Remove(sourceNodeId);
        if (!parallelJoinArrivals.TryGetValue(joinNodeId, out var arrivals)) parallelJoinArrivals[joinNodeId] = arrivals = [];
        arrivals.Add(sourceNodeId);
        var expectedSources = snapshotConnections.Where(x => x.TargetNodeId == joinNodeId).Select(x => x.SourceNodeId).ToHashSet();
        if (!expectedSources.IsSubsetOf(arrivals))
        {
            CurrentNodeId = activeNodeIds.OrderBy(x => x).FirstOrDefault();
            return false;
        }
        parallelJoinArrivals.Remove(joinNodeId);
        activeNodeIds.Add(joinNodeId);
        CurrentNodeId = joinNodeId;
        return true;
    }

    public WorkflowConnection AdvanceCondition(IReadOnlyDictionary<string, object?> fields)
    {
        var transition = SelectConditionTransition(CurrentNodeId, fields);
        AdvanceActiveNode(CurrentNodeId, transition.TargetNodeId, transition.ConditionKey);
        return transition;
    }

    public WorkflowConnection SelectConditionTransition(Guid conditionNodeId, IReadOnlyDictionary<string, object?> fields)
    {
        var transition = TrySelectConditionTransition(conditionNodeId, fields);
        return transition ?? throw new InvalidOperationException("条件节点没有命中分支，也没有可用默认分支。");
    }

    public WorkflowConnection? TrySelectConditionTransition(Guid conditionNodeId, IReadOnlyDictionary<string, object?> fields)
    {
        if (!activeNodeIds.Contains(conditionNodeId) || GetNodeType(conditionNodeId) != WorkflowNodeType.Condition)
            throw new InvalidOperationException("指定节点不是活动的条件节点。");
        var branchKey = WorkflowConditionEvaluator.SelectBranch(GetNodeConfig(conditionNodeId), fields);
        if (branchKey is null) return null;
        var transition = GetOutgoingTransitions(conditionNodeId).SingleOrDefault(x => string.Equals(x.ConditionKey, branchKey, StringComparison.OrdinalIgnoreCase));
        if (transition is null) throw new InvalidOperationException($"条件节点选择的分支不存在：{branchKey}。");
        return transition;
    }

    /// <summary>执行一次显式循环，达到上限后走 exit，否则走 repeat。</summary>
    public WorkflowConnection AdvanceLoop(Guid loopNodeId)
    {
        if (!activeNodeIds.Contains(loopNodeId) || GetNodeType(loopNodeId) != WorkflowNodeType.Loop)
            throw new InvalidOperationException("指定节点不是活动的 Loop 节点。");
        var configuration = WorkflowLoopConfiguration.Parse(GetNodeConfig(loopNodeId));
        var count = checked((loopIterations.TryGetValue(loopNodeId, out var previous) ? previous : 0) + 1);
        loopIterations[loopNodeId] = count;
        var key = count < configuration.MaxIterations ? WorkflowLoopConfiguration.RepeatKey : WorkflowLoopConfiguration.ExitKey;
        var transition = GetOutgoingTransitions(loopNodeId).SingleOrDefault(x => string.Equals(x.ConditionKey, key, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Loop 节点缺少“{key}”分支连线。");
        if (GetNodeType(transition.TargetNodeId) == WorkflowNodeType.ParallelJoin)
        {
            ArriveAtParallelJoin(loopNodeId, transition.TargetNodeId);
            return transition;
        }
        AdvanceActiveNode(loopNodeId, transition.TargetNodeId, transition.ConditionKey);
        return transition;
    }

    public WorkflowActionDefinition? GetNodeAction(Guid nodeId, WorkflowActionTrigger trigger)
    {
        if (!snapshotNodeConfigs.TryGetValue(nodeId, out var config))
            throw new InvalidOperationException("流程实例快照中不存在该节点。");
        return WorkflowNodeActionConfiguration.Parse(config).Get(trigger);
    }

    private static JsonElement FindProperty(JsonElement element, string name)
        => element.EnumerateObject().FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;

    private static void ValidateSnapshotIdentity(string snapshotJson, Guid definitionId, string definitionCode, int definitionVersion)
    {
        using var document = JsonDocument.Parse(snapshotJson);
        var root = document.RootElement;
        var snapshotId = FindProperty(root, "Id");
        if (snapshotId.ValueKind != JsonValueKind.Undefined && snapshotId.ValueKind != JsonValueKind.Null
            && snapshotId.GetGuid() != definitionId)
            throw new ArgumentException("流程实例快照与实例定义 ID 不一致。", nameof(snapshotJson));

        var snapshotCode = FindProperty(root, "Code");
        if (snapshotCode.ValueKind != JsonValueKind.Undefined && snapshotCode.ValueKind != JsonValueKind.Null
            && !string.Equals(snapshotCode.GetString(), definitionCode.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("流程实例快照与实例定义编码不一致。", nameof(snapshotJson));

        var snapshotVersion = FindProperty(root, "VersionNumber");
        if (snapshotVersion.ValueKind != JsonValueKind.Undefined && snapshotVersion.ValueKind != JsonValueKind.Null
            && snapshotVersion.GetInt32() != definitionVersion)
            throw new ArgumentException("流程实例快照与实例定义版本不一致。", nameof(snapshotJson));
    }

    private static (IReadOnlyDictionary<Guid, string> NodeConfigs, IReadOnlyDictionary<Guid, WorkflowNodeType> NodeTypes, IReadOnlyDictionary<Guid, string> NodeNames, IReadOnlyList<WorkflowConnection> Connections) ParseSnapshot(string snapshotJson)
    {
        using var document = JsonDocument.Parse(snapshotJson);
        var nodes = FindProperty(document.RootElement, "Nodes");
        var connections = FindProperty(document.RootElement, "Connections");
        if (nodes.ValueKind != JsonValueKind.Array || connections.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("流程实例快照缺少节点或连线集合。", nameof(snapshotJson));

        var nodeConfigs = new Dictionary<Guid, string>();
        var nodeTypes = new Dictionary<Guid, WorkflowNodeType>();
        var nodeNames = new Dictionary<Guid, string>();
        foreach (var node in nodes.EnumerateArray())
        {
            var nodeId = FindProperty(node, "Id").GetGuid();
            if (nodeId == Guid.Empty || nodeConfigs.ContainsKey(nodeId))
                throw new ArgumentException("流程实例快照包含无效或重复节点。", nameof(snapshotJson));
            nodeConfigs[nodeId] = FindProperty(node, "ConfigJson").GetString() ?? "{}";
            nodeTypes[nodeId] = ReadNodeType(FindProperty(node, "Type"));
            nodeNames[nodeId] = FindProperty(node, "Name").GetString() ?? string.Empty;
        }

        var transitions = connections.EnumerateArray().Select(connection => new WorkflowConnection(
            FindProperty(connection, "SourceNodeId").GetGuid(),
            FindProperty(connection, "TargetNodeId").GetGuid(),
            ReadNullableString(FindProperty(connection, "ConditionKey")))).ToArray();
        if (transitions.Any(x => x.SourceNodeId == Guid.Empty || x.TargetNodeId == Guid.Empty || !nodeConfigs.ContainsKey(x.SourceNodeId) || !nodeConfigs.ContainsKey(x.TargetNodeId)))
            throw new ArgumentException("流程实例快照包含无效连线。", nameof(snapshotJson));
        if (transitions.Where((transition, index) => transitions.Skip(index + 1).Any(other =>
                other.SourceNodeId == transition.SourceNodeId
                && other.TargetNodeId == transition.TargetNodeId
                && string.Equals(other.ConditionKey?.Trim(), transition.ConditionKey?.Trim(), StringComparison.OrdinalIgnoreCase))).Any())
            throw new ArgumentException("流程实例快照包含重复连线。", nameof(snapshotJson));

        ValidateSnapshotGraph(nodeConfigs, nodeTypes, nodeNames, transitions, snapshotJson);
        return (nodeConfigs, nodeTypes, nodeNames, transitions);
    }

    private static void ValidateSnapshotGraph(
        IReadOnlyDictionary<Guid, string> nodeConfigs,
        IReadOnlyDictionary<Guid, WorkflowNodeType> nodeTypes,
        IReadOnlyDictionary<Guid, string> nodeNames,
        IReadOnlyList<WorkflowConnection> connections,
        string snapshotJson)
    {
        WorkflowDefinition definition;
        try
        {
            definition = new WorkflowDefinition("INSTANCE_SNAPSHOT", "流程实例快照");
            foreach (var nodeId in nodeTypes.Keys)
                definition.AddNode(nodeId, nodeTypes[nodeId], nodeNames[nodeId], configJson: nodeConfigs[nodeId]);
            foreach (var connection in connections)
                definition.Connect(connection.SourceNodeId, connection.TargetNodeId, connection.ConditionKey);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException($"流程实例快照节点数据无效：{ex.Message}", nameof(snapshotJson), ex);
        }

        var result = definition.Validate();
        if (!result.IsValid)
            throw new ArgumentException($"流程实例快照图校验失败：{string.Join("；", result.Errors)}", nameof(snapshotJson));
    }

    private static string? ReadNullableString(JsonElement element)
        => element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : element.GetString();

    private static WorkflowNodeType ReadNodeType(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            return Enum.Parse<WorkflowNodeType>(element.GetString() ?? string.Empty, ignoreCase: true);
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var numericValue) && Enum.IsDefined(typeof(WorkflowNodeType), numericValue))
            return (WorkflowNodeType)numericValue;
        throw new ArgumentException("流程实例快照包含无效节点类型。");
    }

    private static HashSet<Guid> ParseActiveNodeIds(string? activeNodeIdsJson, Guid currentNodeId, IReadOnlyDictionary<Guid, WorkflowNodeType> nodeTypes)
    {
        var known = nodeTypes.Keys.ToHashSet();
        if (!known.Contains(currentNodeId))
            throw new ArgumentException("流程实例活动节点快照无效。", nameof(currentNodeId));
        if (string.IsNullOrWhiteSpace(activeNodeIdsJson) || activeNodeIdsJson.Trim() == "[]") return [currentNodeId];
        try
        {
            var values = JsonSerializer.Deserialize<Guid[]>(activeNodeIdsJson, JsonOptions) ?? [];
            var result = values.ToHashSet();
            if (result.Count == 0
                || !result.Contains(currentNodeId)
                || result.Any(x => x == Guid.Empty || !known.Contains(x))
                || (result.Count > 1 && result.Any(x => nodeTypes[x] == WorkflowNodeType.End)))
                throw new ArgumentException("流程实例活动节点快照无效。", nameof(activeNodeIdsJson));
            return result;
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("流程实例活动节点快照必须是有效 JSON。", nameof(activeNodeIdsJson), ex);
        }
    }

    private static Dictionary<Guid, HashSet<Guid>> ParseParallelJoinArrivals(
        string? json,
        IReadOnlySet<Guid> activeNodeIds,
        IEnumerable<Guid> knownNodeIds,
        IReadOnlyDictionary<Guid, WorkflowNodeType> nodeTypes,
        IReadOnlyList<WorkflowConnection> connections)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        var known = knownNodeIds.ToHashSet();
        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<Guid, Guid[]>>(json, JsonOptions) ?? [];
            if (raw.Any(x =>
            {
                var expectedSources = connections.Where(connection => connection.TargetNodeId == x.Key).Select(connection => connection.SourceNodeId).ToHashSet();
                return x.Key == Guid.Empty
                    || !known.Contains(x.Key)
                    || !nodeTypes.TryGetValue(x.Key, out var nodeType)
                    || nodeType != WorkflowNodeType.ParallelJoin
                    || activeNodeIds.Contains(x.Key)
                    || x.Value.Length == 0
                    || x.Value.Any(y => y == Guid.Empty || !known.Contains(y) || activeNodeIds.Contains(y) || !connections.Any(connection => connection.SourceNodeId == y && connection.TargetNodeId == x.Key))
                    || expectedSources.IsSubsetOf(x.Value);
            }))
                throw new ArgumentException("流程实例并行汇聚快照无效。", nameof(json));
            return raw.ToDictionary(x => x.Key, x => x.Value.ToHashSet());
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("流程实例并行汇聚快照必须是有效 JSON。", nameof(json), ex);
        }
    }

    private static Dictionary<Guid, int> ParseLoopIterations(
        string? json,
        IEnumerable<Guid> knownNodeIds,
        IReadOnlyDictionary<Guid, WorkflowNodeType> nodeTypes,
        IReadOnlyDictionary<Guid, string> nodeConfigs)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        var known = knownNodeIds.ToHashSet();
        try
        {
            var values = JsonSerializer.Deserialize<Dictionary<Guid, int>>(json, JsonOptions) ?? [];
            if (values.Any(x => !known.Contains(x.Key)
                || x.Value < 0
                || !nodeTypes.TryGetValue(x.Key, out var nodeType)
                || nodeType != WorkflowNodeType.Loop
                || !nodeConfigs.TryGetValue(x.Key, out var config)
                || x.Value > WorkflowLoopConfiguration.Parse(config).MaxIterations))
                throw new ArgumentException("流程实例循环计数包含无效节点或次数。", nameof(json));
            return values;
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("流程实例循环计数必须是有效 JSON。", nameof(json), ex);
        }
    }

    private static Dictionary<Guid, string[]> ParseApprovalAssignees(string? json, IEnumerable<Guid> knownNodeIds, IReadOnlyDictionary<Guid, WorkflowNodeType> nodeTypes)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        var known = knownNodeIds.ToHashSet();
        try
        {
            var values = JsonSerializer.Deserialize<Dictionary<Guid, string[]>>(json, JsonOptions) ?? [];
            if (values.Any(x => x.Key == Guid.Empty
                || !known.Contains(x.Key)
                || !nodeTypes.TryGetValue(x.Key, out var nodeType)
                || nodeType != WorkflowNodeType.Approval
                || x.Value is null
                || x.Value.Length == 0
                || !x.Value.SequenceEqual(NormalizeApprovalAssignees(x.Value), StringComparer.OrdinalIgnoreCase)))
                throw new ArgumentException("流程实例审批人快照无效。", nameof(json));
            return values.ToDictionary(x => x.Key, x => x.Value);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("流程实例审批人快照必须是有效 JSON。", nameof(json), ex);
        }
    }

    private static string[] NormalizeApprovalAssignees(IEnumerable<string> assignees)
        => assignees
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private void Finish(WorkflowInstanceStatus status, DateTime? completedAt)
    {
        if (Status != WorkflowInstanceStatus.Running) throw new InvalidOperationException("已结束的流程实例不能重复处理。");
        Status = status;
        CompletedAt = completedAt ?? DateTime.Now;
    }
}
