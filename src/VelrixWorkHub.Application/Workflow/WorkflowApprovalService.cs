using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Workflow;

/// <summary>
/// 为业务应用层提供统一的审批状态查询与关键动作门禁。
/// </summary>
public sealed class WorkflowApprovalService(WorkflowBindingService bindings)
{
    public WorkflowInstance? Latest(string definitionCode, string businessType, Guid businessId)
        => bindings.List(businessType, businessId)
            .Where(x => x.DefinitionCode.Equals(definitionCode, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefault();

    public bool IsCompleted(string definitionCode, string businessType, Guid businessId)
        => Latest(definitionCode, businessType, businessId)?.Status == WorkflowInstanceStatus.Completed;

    public void RequireCompleted(string definitionCode, string businessType, Guid businessId, string actionName)
    {
        var workflow = Latest(definitionCode, businessType, businessId);
        if (workflow?.Status == WorkflowInstanceStatus.Completed) return;

        var message = workflow?.Status switch
        {
            WorkflowInstanceStatus.Running => "审批正在进行中",
            WorkflowInstanceStatus.Rejected => "审批已拒绝，请重新发起审批",
            WorkflowInstanceStatus.Cancelled => "审批已撤回，请重新发起审批",
            _ => "尚未发起审批"
        };
        throw new InvalidOperationException($"{actionName}前必须完成审批：{message}。");
    }

    public void RequireNotRunning(string definitionCode, string businessType, Guid businessId, string actionName)
    {
        if (bindings.List(businessType, businessId).Any(x => x.DefinitionCode.Equals(definitionCode, StringComparison.OrdinalIgnoreCase) && x.Status == WorkflowInstanceStatus.Running))
            throw new InvalidOperationException($"审批进行中，暂不能{actionName}。");
    }
}
