using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.ProcurementRequests;

public sealed class ProcurementRequestWorkflowActionHandler(
    IOaProcurementRequestRepository repository,
    IOaProcurementRequestWorkflowApprover? workflowApprover = null) : IWorkflowActionHandler
{
    public bool CanHandle(string businessType) => businessType.Equals(nameof(OaProcurementRequest), StringComparison.OrdinalIgnoreCase);

    public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
    {
        if (action.Type != WorkflowActionType.SetField || !action.Field.Equals(nameof(OaProcurementRequest.Status), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"采购申请流程不支持动作：{action.Type}/{action.Field}/{action.Value}。");
        var item = repository.Get(context.Instance.BusinessId) ?? throw new InvalidOperationException("流程关联的采购申请不存在或已被删除。");
        switch (action.Value)
        {
            case nameof(OaProcurementRequestStatus.Approved):
                if (workflowApprover is not null) workflowApprover.ApplyApproval(item); else { item.Approve(); repository.Update(item); }
                break;
            case nameof(OaProcurementRequestStatus.Rejected):
                if (workflowApprover is not null) workflowApprover.ApplyRejection(item, context.Reason); else { item.Reject(context.Reason); repository.Update(item); }
                break;
            default:
                throw new InvalidOperationException($"采购申请流程不支持状态回写：{action.Value}。");
        }
    }
}
