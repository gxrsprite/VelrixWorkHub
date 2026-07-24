using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Workflow;

public sealed record WorkflowActionContext(WorkflowInstance Instance, WorkflowActionTrigger Trigger, string? Reason, string? Actor = null);

public interface IWorkflowActionHandler
{
    bool CanHandle(string businessType);
    void Execute(WorkflowActionContext context, WorkflowActionDefinition action);
}

/// <summary>
/// 执行流程定义声明的业务动作。引擎不反射、不直接写业务表，只调用业务模块注册的处理器。
/// </summary>
public sealed class WorkflowActionExecutor(IEnumerable<IWorkflowActionHandler> handlers)
{
    public bool Execute(WorkflowInstance instance, Guid nodeId, WorkflowActionTrigger trigger, string? reason = null, string? actor = null)
    {
        var action = instance.GetNodeAction(nodeId, trigger);
        if (action is null) return false;

        Execute(instance, nodeId, trigger, action, reason, actor);
        return true;
    }

    public bool ExecuteNode(WorkflowInstance instance, Guid nodeId, string? reason = null, string? actor = null)
    {
        var action = WorkflowNodeActionConfiguration.ParseNodeAction(instance.GetNodeConfig(nodeId));
        if (action is null) return false;

        Execute(instance, nodeId, WorkflowActionTrigger.Approved, action, reason, actor);
        return true;
    }

    private void Execute(WorkflowInstance instance, Guid nodeId, WorkflowActionTrigger trigger, WorkflowActionDefinition action, string? reason, string? actor = null)
    {
        action.Validate();

        var matching = handlers.Where(x => x.CanHandle(instance.BusinessType)).ToArray();
        if (matching.Length != 1) throw new InvalidOperationException($"业务类型“{instance.BusinessType}”没有唯一的流程动作处理器。处理器数量：{matching.Length}。");
        matching[0].Execute(new WorkflowActionContext(instance, trigger, reason, actor), action);
    }
}
