using System.Text.Json;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Workflow;

/// <summary>将流程节点配置解析为实例级审批人，避免页面或业务模块参与待办分派。</summary>
public interface IWorkflowApproverResolver
{
    IReadOnlyList<string> Resolve(WorkflowInstance instance, string nodeConfigJson);
}

public interface IWorkflowRoleApproverLookup
{
    IReadOnlyList<string> FindUsernames(IReadOnlyCollection<string> roleNames);
}

public interface IWorkflowOrganizationApproverLookup
{
    IReadOnlyList<string> FindUsernames(IReadOnlyCollection<string> organizationNames);
}

/// <summary>由业务模块按实例上下文提供审批人字段值，Workflow 不直接读取业务表。</summary>
public interface IWorkflowBusinessApproverLookup
{
    IReadOnlyList<string> FindUsernames(WorkflowInstance instance, IReadOnlyCollection<string> fieldNames);
}

public interface IWorkflowBusinessApproverSource
{
    bool CanHandle(WorkflowInstance instance);
    IReadOnlyList<string> FindUsernames(WorkflowInstance instance, IReadOnlyCollection<string> fieldNames);
}

public sealed class DefaultWorkflowBusinessApproverLookup(IEnumerable<IWorkflowBusinessApproverSource> sources) : IWorkflowBusinessApproverLookup
{
    public IReadOnlyList<string> FindUsernames(WorkflowInstance instance, IReadOnlyCollection<string> fieldNames)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(fieldNames);
        var handlers = sources.Where(x => x.CanHandle(instance)).ToArray();
        if (handlers.Length == 0)
            throw new InvalidOperationException($"业务类型“{instance.BusinessType}”未注册审批人业务字段查询器。");

        return handlers.SelectMany(x => x.FindUsernames(instance, fieldNames))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

/// <summary>
/// 兼容既有 approver/approvers 用户名配置，并提供 $initiator 发起人占位符。
/// 角色、组织和业务字段来源通过同一契约扩展。
/// </summary>
public sealed class DefaultWorkflowApproverResolver(IWorkflowRoleApproverLookup? roleLookup = null, IWorkflowOrganizationApproverLookup? organizationLookup = null, IWorkflowBusinessApproverLookup? businessLookup = null) : IWorkflowApproverResolver
{
    public IReadOnlyList<string> Resolve(WorkflowInstance instance, string nodeConfigJson)
    {
        ArgumentNullException.ThrowIfNull(instance);
        using var document = JsonDocument.Parse(nodeConfigJson);
        var root = document.RootElement;
        var values = new List<string>();
        if (root.TryGetProperty("approver", out var approver) && approver.ValueKind == JsonValueKind.String)
            values.Add(approver.GetString() ?? string.Empty);
        if (root.TryGetProperty("approvers", out var approvers) && approvers.ValueKind == JsonValueKind.Array)
            values.AddRange(approvers.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString() ?? string.Empty));
        if (root.TryGetProperty("approverRoles", out var roles) && roles.ValueKind == JsonValueKind.Array)
        {
            var roleNames = roles.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString()?.Trim() ?? string.Empty)
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (roleNames.Length > 0)
            {
                if (roleLookup is null) throw new InvalidOperationException("审批节点配置了 approverRoles，但当前未注册角色审批人查询器。");
                values.AddRange(roleLookup.FindUsernames(roleNames));
            }
        }
        if (root.TryGetProperty("approverOrgs", out var organizations) && organizations.ValueKind == JsonValueKind.Array)
        {
            var names = organizations.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString()?.Trim() ?? string.Empty)
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (names.Length > 0)
            {
                if (organizationLookup is null) throw new InvalidOperationException("审批节点配置了 approverOrgs，但当前未注册组织审批人查询器。");
                values.AddRange(organizationLookup.FindUsernames(names));
            }
        }
        if (root.TryGetProperty("approverBusinessFields", out var businessFields) && businessFields.ValueKind == JsonValueKind.Array)
        {
            var names = businessFields.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString()?.Trim() ?? string.Empty)
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (names.Length > 0)
            {
                if (businessLookup is null) throw new InvalidOperationException("审批节点配置了 approverBusinessFields，但当前未注册业务字段审批人查询器。");
                values.AddRange(businessLookup.FindUsernames(instance, names));
            }
        }
        return values
            .Select(value => value.Trim())
            .Select(value => value.Equals("$initiator", StringComparison.OrdinalIgnoreCase) ? instance.StartedBy : value)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
