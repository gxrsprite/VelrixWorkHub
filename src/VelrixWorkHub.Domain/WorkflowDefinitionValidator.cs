using System.Text.Json;

namespace VelrixWorkHub.Domain;

public sealed record WorkflowValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public static class WorkflowDefinitionValidator
{
    public static WorkflowValidationResult Validate(WorkflowDefinition definition)
    {
        var errors = new List<string>();
        var nodes = definition.Nodes;
        var nodeIds = nodes.Select(x => x.Id).ToHashSet();
        var nodeById = nodes.ToDictionary(x => x.Id);
        var starts = nodes.Where(x => x.Type == WorkflowNodeType.Start).ToArray();
        var ends = nodes.Where(x => x.Type == WorkflowNodeType.End).ToArray();
        if (starts.Length != 1) errors.Add("流程必须且只能有一个开始节点。");
        if (ends.Length == 0) errors.Add("流程至少需要一个结束节点。");

        foreach (var connection in definition.Connections)
        {
            if (!nodeIds.Contains(connection.SourceNodeId) || !nodeIds.Contains(connection.TargetNodeId)) errors.Add("流程连线引用了不存在的节点。");
        }

        foreach (var node in nodes)
        {
            if (node.Type == WorkflowNodeType.Start && definition.Connections.Any(x => x.TargetNodeId == node.Id)) errors.Add($"开始节点“{node.Name}”不能有入边。");
            if (node.Type == WorkflowNodeType.End && definition.Connections.Any(x => x.SourceNodeId == node.Id)) errors.Add($"结束节点“{node.Name}”不能有出边。");
            var nodeOutgoing = definition.Connections.Where(x => x.SourceNodeId == node.Id).ToArray();
            var nodeIncoming = definition.Connections.Where(x => x.TargetNodeId == node.Id).ToArray();
            if (node.Type == WorkflowNodeType.End && nodeIncoming.Any(x => nodeById.TryGetValue(x.SourceNodeId, out var source) && source.Type == WorkflowNodeType.ParallelSplit))
                errors.Add($"结束节点“{node.Name}”不能直接作为并行拆分分支，必须先经过并行汇聚节点。");
            if (node.Type != WorkflowNodeType.End && nodeOutgoing.Length == 0)
                errors.Add($"节点“{node.Name}”缺少出边，流程无法继续推进。");
            if (node.Type == WorkflowNodeType.Condition)
            {
                if (nodeOutgoing.Any(x => string.IsNullOrWhiteSpace(x.ConditionKey))) errors.Add($"条件节点“{node.Name}”的每条出边都必须配置分支键。");
                if (nodeOutgoing
                    .Where(x => !string.IsNullOrWhiteSpace(x.ConditionKey))
                    .GroupBy(x => x.ConditionKey!.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Any(group => group.Count() > 1))
                    errors.Add($"条件节点“{node.Name}”的分支键不能重复，否则运行时无法唯一选择连线。");
            }
            else if (node.Type == WorkflowNodeType.ParallelSplit)
            {
                if (nodeOutgoing.Length < 2
                    || nodeOutgoing.Any(x => !string.IsNullOrWhiteSpace(x.ConditionKey))
                    || nodeOutgoing.Select(x => x.TargetNodeId).Distinct().Count() < 2)
                    errors.Add($"并行拆分节点“{node.Name}”至少需要两条指向不同目标的无条件出边。");
                if (nodeOutgoing.Any(x => nodeById.TryGetValue(x.TargetNodeId, out var target) && target.Type == WorkflowNodeType.ParallelJoin))
                    errors.Add($"并行拆分节点“{node.Name}”不能直接连接并行汇聚节点，必须先经过实际分支节点。");
            }
            else if (node.Type == WorkflowNodeType.ParallelJoin)
            {
                if (nodeIncoming.Length < 2) errors.Add($"并行汇聚节点“{node.Name}”至少需要两条入边。");
                if (nodeOutgoing.Length != 1 || nodeOutgoing.Any(x => !string.IsNullOrWhiteSpace(x.ConditionKey))) errors.Add($"并行汇聚节点“{node.Name}”必须只有一条无条件出边。");
            }
            else if (node.Type == WorkflowNodeType.Loop)
            {
                var keys = nodeOutgoing.Select(x => x.ConditionKey?.Trim()).ToArray();
                if (nodeOutgoing.Length != 2
                    || keys.Any(string.IsNullOrWhiteSpace)
                    || !keys.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals([WorkflowLoopConfiguration.RepeatKey, WorkflowLoopConfiguration.ExitKey]))
                    errors.Add($"Loop 节点“{node.Name}”必须且只能包含 repeat 与 exit 两条分支出边。");
            }
            else
            {
                if (node.Type == WorkflowNodeType.Start && nodeOutgoing.Length != 1)
                    errors.Add($"开始节点“{node.Name}”必须只有一条无条件出边。");
                if (nodeOutgoing.Count(x => string.IsNullOrWhiteSpace(x.ConditionKey)) > 1) errors.Add($"节点“{node.Name}”只能有一条无条件自动出边。");
                if (nodeOutgoing.Any(x => !string.IsNullOrWhiteSpace(x.ConditionKey))) errors.Add($"只有条件节点可以使用条件分支出边，节点“{node.Name}”存在非法条件键。");
            }
            ValidateNodeConfig(node, nodes, errors);
            if (node.Type == WorkflowNodeType.Condition)
            {
                var branches = definition.Connections
                    .Where(x => x.SourceNodeId == node.Id)
                    .Select(x => x.ConditionKey?.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (branches.Length < 2) errors.Add($"条件节点“{node.Name}”至少需要两个不重复的分支条件。");
                try
                {
                    var configuredKeys = WorkflowConditionEvaluator.GetBranchKeys(node.ConfigJson).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var connectedKeys = definition.Connections.Where(x => x.SourceNodeId == node.Id).Select(x => x.ConditionKey?.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    foreach (var key in configuredKeys.Except(connectedKeys, StringComparer.OrdinalIgnoreCase)) errors.Add($"条件节点“{node.Name}”配置的分支“{key}”没有对应连线。");
                    foreach (var key in connectedKeys.Except(configuredKeys, StringComparer.OrdinalIgnoreCase)) errors.Add($"条件节点“{node.Name}”连线分支“{key}”没有对应配置。");
                }
                catch (Exception ex) when (ex is JsonException or ArgumentException)
                { errors.Add($"条件节点“{node.Name}”的分支键配置无效：{ex.Message}"); }
            }
        }

        var outgoing = definition.Connections.Where(x => nodeIds.Contains(x.SourceNodeId) && nodeIds.Contains(x.TargetNodeId)).GroupBy(x => x.SourceNodeId).ToDictionary(x => x.Key, x => x.Select(y => y.TargetNodeId).ToArray());
        ValidateParallelJoinCoverage(nodes, definition.Connections, outgoing, errors);
        ValidateConditionJoinFanOut(nodes, definition.Connections, outgoing, errors);
        var reachable = starts.Length == 1 ? Reachable(starts[0].Id, outgoing) : [];
        foreach (var node in nodes.Where(x => !reachable.Contains(x.Id))) errors.Add($"节点“{node.Name}”从开始节点不可达。");
        if (ends.Any(end => !reachable.Contains(end.Id))) errors.Add("存在不可达的结束节点。");
        if (ends.Length > 0)
        {
            var incoming = definition.Connections
                .Where(x => nodeIds.Contains(x.SourceNodeId) && nodeIds.Contains(x.TargetNodeId))
                .GroupBy(x => x.TargetNodeId)
                .ToDictionary(x => x.Key, x => x.Select(y => y.SourceNodeId).ToArray());
            var canReachEnd = Reachable(ends.Select(x => x.Id), incoming);
            foreach (var node in nodes.Where(x => reachable.Contains(x.Id) && !canReachEnd.Contains(x.Id))) errors.Add($"节点“{node.Name}”无法到达结束节点。");
        }
        var uncontrolledOutgoing = definition.Connections
            .Where(x => nodeIds.Contains(x.SourceNodeId) && nodeIds.Contains(x.TargetNodeId))
            .Where(x => !(nodes.Single(node => node.Id == x.SourceNodeId).Type == WorkflowNodeType.Loop
                && string.Equals(x.ConditionKey, WorkflowLoopConfiguration.RepeatKey, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(x => x.SourceNodeId)
            .ToDictionary(x => x.Key, x => x.Select(y => y.TargetNodeId).ToArray());
        if (HasCycle(nodes, uncontrolledOutgoing)) errors.Add("流程循环必须经过 Loop 节点的 repeat 分支。");
        return new WorkflowValidationResult(errors.Distinct().ToArray());
    }

    private static void ValidateParallelJoinCoverage(
        IReadOnlyList<WorkflowNode> nodes,
        IReadOnlyList<WorkflowConnection> connections,
        IReadOnlyDictionary<Guid, Guid[]> outgoing,
        List<string> errors)
    {
        foreach (var join in nodes.Where(x => x.Type == WorkflowNodeType.ParallelJoin))
        {
            var sources = connections.Where(x => x.TargetNodeId == join.Id).Select(x => x.SourceNodeId).Distinct().ToArray();
            if (sources.Length < 2) continue;
            var hasCoveringSplit = nodes
                .Where(x => x.Type == WorkflowNodeType.ParallelSplit)
                .Any(split =>
                {
                    var reachableFromSplit = Reachable(split.Id, outgoing);
                    return sources.All(reachableFromSplit.Contains);
                });
            if (!hasCoveringSplit)
                errors.Add($"并行汇聚节点“{join.Name}”的所有入边必须位于同一个上游并行拆分范围内，否则运行时可能永久等待。");
        }
    }

    private static void ValidateConditionJoinFanOut(
        IReadOnlyList<WorkflowNode> nodes,
        IReadOnlyList<WorkflowConnection> connections,
        IReadOnlyDictionary<Guid, Guid[]> outgoing,
        List<string> errors)
    {
        var joins = nodes.Where(x => x.Type == WorkflowNodeType.ParallelJoin).ToArray();
        foreach (var condition in nodes.Where(x => x.Type == WorkflowNodeType.Condition))
        {
            foreach (var join in joins)
            {
                var sources = connections.Where(x => x.TargetNodeId == join.Id).Select(x => x.SourceNodeId).Distinct().ToHashSet();
                sources.Remove(condition.Id); // Condition 直接连接 Join 时，运行时以 Condition 本身作为单一来源。
                var reachableSources = ReachableWithoutEnteringParallelSplit(condition.Id, outgoing, nodes)
                    .Where(sources.Contains)
                    .ToArray();
                if (reachableSources.Length > 1)
                    errors.Add($"条件节点“{condition.Name}”在未经过新的并行拆分时分流到并行汇聚“{join.Name}”的多个入边，互斥分支会导致 Join 永久等待。");
            }
        }
    }

    private static HashSet<Guid> ReachableWithoutEnteringParallelSplit(
        Guid start,
        IReadOnlyDictionary<Guid, Guid[]> outgoing,
        IReadOnlyList<WorkflowNode> nodes)
    {
        var nodeById = nodes.ToDictionary(x => x.Id);
        var result = new HashSet<Guid>();
        var pending = new Stack<Guid>([start]);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!result.Add(current)) continue;
            if (current != start && nodeById.TryGetValue(current, out var node) && node.Type == WorkflowNodeType.ParallelSplit)
                continue;
            if (outgoing.TryGetValue(current, out var targets)) foreach (var target in targets) pending.Push(target);
        }
        return result;
    }

    private static void ValidateNodeConfig(WorkflowNode node, IReadOnlyList<WorkflowNode> nodes, List<string> errors)
    {
        if (node.Type is WorkflowNodeType.Start or WorkflowNodeType.End) return;
        using var document = JsonDocument.Parse(node.ConfigJson);
        if (node.Type == WorkflowNodeType.Approval)
            {
            var hasSingle = document.RootElement.TryGetProperty("approver", out var single)
                && single.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(single.GetString());
            var hasMany = document.RootElement.TryGetProperty("approvers", out var many)
                && many.ValueKind == JsonValueKind.Array
                && many.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(x.GetString()));
            var hasRoles = document.RootElement.TryGetProperty("approverRoles", out var roles)
                && roles.ValueKind == JsonValueKind.Array
                && roles.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(x.GetString()));
            var hasOrgs = document.RootElement.TryGetProperty("approverOrgs", out var organizations)
                && organizations.ValueKind == JsonValueKind.Array
                && organizations.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(x.GetString()));
            var hasBusinessFields = document.RootElement.TryGetProperty("approverBusinessFields", out var businessFields)
                && businessFields.ValueKind == JsonValueKind.Array
                && businessFields.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(x.GetString()));
            if (!hasSingle && !hasMany && !hasRoles && !hasOrgs && !hasBusinessFields) errors.Add($"节点“{node.Name}”缺少配置“approver”、“approvers”、“approverRoles”、“approverOrgs”或“approverBusinessFields”。");
            try { _ = WorkflowNodeActionConfiguration.Parse(node.ConfigJson); }
            catch (Exception ex) when (ex is JsonException or ArgumentException or ArgumentOutOfRangeException)
            { errors.Add($"节点“{node.Name}”的流程动作配置无效：{ex.Message}"); }
            try { _ = WorkflowApprovalConfiguration.ParseMode(node.ConfigJson); }
            catch (Exception ex) when (ex is JsonException or ArgumentException or ArgumentOutOfRangeException)
            { errors.Add($"节点“{node.Name}”的审批策略配置无效：{ex.Message}"); }
            try
            {
                foreach (var targetNodeId in WorkflowReturnConfiguration.ParseTargets(node.ConfigJson))
                {
                    var target = nodes.SingleOrDefault(x => x.Id == targetNodeId);
                    if (target is null) errors.Add($"节点“{node.Name}”的回退目标不存在。");
                    else if (target.Type != WorkflowNodeType.Approval) errors.Add($"节点“{node.Name}”的回退目标必须是审批节点。");
                    else if (target.Id == node.Id) errors.Add($"节点“{node.Name}”不能回退到自身。");
                }
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or ArgumentOutOfRangeException)
            { errors.Add($"节点“{node.Name}”的回退配置无效：{ex.Message}"); }
            return;
        }

        if (node.Type == WorkflowNodeType.Loop)
        {
            try { _ = WorkflowLoopConfiguration.Parse(node.ConfigJson); }
            catch (Exception ex) when (ex is JsonException or ArgumentException or ArgumentOutOfRangeException)
            { errors.Add($"节点“{node.Name}”的循环配置无效：{ex.Message}"); }
            return;
        }

        var requiredKey = node.Type switch
        {
            WorkflowNodeType.Notification => "recipients",
            WorkflowNodeType.BusinessAction => "action",
            WorkflowNodeType.Condition => "expression",
            _ => string.Empty
        };
        if (node.Type == WorkflowNodeType.Condition)
        {
            var hasExpression = document.RootElement.TryGetProperty("expression", out var expression)
                && expression.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(expression.GetString());
            var hasBranches = document.RootElement.TryGetProperty("branches", out var branches)
                && branches.ValueKind == JsonValueKind.Array
                && branches.GetArrayLength() > 0;
            if (!hasExpression && !hasBranches) errors.Add($"节点“{node.Name}”缺少配置“expression”或“branches”。");
            try { WorkflowConditionEvaluator.Validate(node.ConfigJson); }
            catch (Exception ex) when (ex is JsonException or ArgumentException or ArgumentOutOfRangeException)
            { errors.Add($"节点“{node.Name}”的条件配置无效：{ex.Message}"); }
        }
        else if (!string.IsNullOrEmpty(requiredKey) && (!document.RootElement.TryGetProperty(requiredKey, out var value) || string.IsNullOrWhiteSpace(value.ToString()))) errors.Add($"节点“{node.Name}”缺少配置“{requiredKey}”。");
        if (node.Type == WorkflowNodeType.BusinessAction)
        {
            try
            {
                if (WorkflowNodeActionConfiguration.ParseNodeAction(node.ConfigJson) is null) errors.Add($"节点“{node.Name}”缺少有效的 action 配置。");
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or ArgumentOutOfRangeException)
            { errors.Add($"节点“{node.Name}”的业务动作配置无效：{ex.Message}"); }
        }
        else if (node.Type == WorkflowNodeType.Notification)
        {
            try { _ = WorkflowNotificationDefinition.Parse(node.ConfigJson, node.Name, "流程通知"); }
            catch (Exception ex) when (ex is JsonException or ArgumentException or ArgumentOutOfRangeException)
            { errors.Add($"节点“{node.Name}”的通知配置无效：{ex.Message}"); }
        }
    }

    private static HashSet<Guid> Reachable(Guid start, IReadOnlyDictionary<Guid, Guid[]> outgoing)
        => Reachable([start], outgoing);

    private static HashSet<Guid> Reachable(IEnumerable<Guid> starts, IReadOnlyDictionary<Guid, Guid[]> outgoing)
    {
        var result = new HashSet<Guid>();
        var pending = new Stack<Guid>(starts);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!result.Add(current)) continue;
            if (outgoing.TryGetValue(current, out var targets)) foreach (var target in targets) pending.Push(target);
        }
        return result;
    }

    private static bool HasCycle(IReadOnlyList<WorkflowNode> nodes, IReadOnlyDictionary<Guid, Guid[]> outgoing)
    {
        var visiting = new HashSet<Guid>();
        var visited = new HashSet<Guid>();
        bool Visit(Guid id)
        {
            if (visiting.Contains(id)) return true;
            if (!visited.Add(id)) return false;
            visiting.Add(id);
            if (outgoing.TryGetValue(id, out var targets) && targets.Any(Visit)) return true;
            visiting.Remove(id);
            return false;
        }
        return nodes.Any(node => Visit(node.Id));
    }
}
