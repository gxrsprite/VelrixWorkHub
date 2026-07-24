using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Settlements;

public sealed class ErpSettlementWorkflowActionHandler(SettlementService settlements) : IWorkflowActionHandler
{
    public bool CanHandle(string businessType) => businessType.Equals(nameof(ErpSettlement), StringComparison.OrdinalIgnoreCase);

    public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
    {
        if (action.Type != WorkflowActionType.SetField || !action.Field.Equals(nameof(ErpSettlement.Status), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"ERP 结算流程不支持动作：{action.Type}/{action.Field}。");

        switch (action.Value)
        {
            case nameof(ErpSettlementStatus.Active):
                settlements.Approve(context.Instance.BusinessId);
                break;
            case nameof(ErpSettlementStatus.Rejected):
                settlements.RejectApproval(context.Instance.BusinessId, context.Reason ?? (context.Trigger == WorkflowActionTrigger.Cancelled ? "审批流程已撤回" : "审批拒绝"));
                break;
            default:
                throw new InvalidOperationException($"ERP 结算状态动作值不受支持：{action.Value}。");
        }
    }
}
