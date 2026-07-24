using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.CashAdvances;

public sealed class CashAdvanceWorkflowActionHandler(
    IOaCashAdvanceRepository repository,
    IOaCashAdvanceWorkflowApprover? workflowApprover = null,
    OaWorkflowOutcomeNotificationService? outcomeNotifications = null) : IWorkflowActionHandler
{
    public bool CanHandle(string businessType) => businessType.Equals(nameof(OaCashAdvance), StringComparison.OrdinalIgnoreCase);

    public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
    {
        if (action.Type != WorkflowActionType.SetField || !action.Field.Equals(nameof(OaCashAdvance.Status), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"借款流程不支持动作：{action.Type}/{action.Field}/{action.Value}。");
        var item = repository.Get(context.Instance.BusinessId) ?? throw new InvalidOperationException("流程关联的借款不存在或已被删除。");
        switch (action.Value)
        {
            case nameof(OaCashAdvanceStatus.Approved):
                if (workflowApprover is not null) workflowApprover.ApplyApproval(item); else { item.Approve(); repository.Update(item); }
                outcomeNotifications?.Publish(item.ApplicantUserId, nameof(OaCashAdvance), item.Id, "借款申请", item.DocumentNo, "/Oa/CashAdvance", context.Instance.Id, "Approved");
                break;
            case nameof(OaCashAdvanceStatus.Rejected):
                if (workflowApprover is not null) workflowApprover.ApplyRejection(item, context.Reason); else { item.Reject(context.Reason); repository.Update(item); }
                outcomeNotifications?.Publish(item.ApplicantUserId, nameof(OaCashAdvance), item.Id, "借款申请", item.DocumentNo, "/Oa/CashAdvance", context.Instance.Id, "Rejected", context.Reason);
                break;
            default:
                throw new InvalidOperationException($"借款流程不支持状态回写：{action.Value}。");
        }
    }
}
