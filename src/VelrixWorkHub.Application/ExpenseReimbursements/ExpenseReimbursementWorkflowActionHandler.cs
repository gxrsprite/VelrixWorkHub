using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.ExpenseReimbursements;

public sealed class ExpenseReimbursementWorkflowActionHandler(
    IOaExpenseReimbursementRepository repository,
    IOaExpenseReimbursementWorkflowApprover? workflowApprover = null,
    OaWorkflowOutcomeNotificationService? outcomeNotifications = null) : IWorkflowActionHandler
{
    public bool CanHandle(string businessType) => businessType.Equals(nameof(OaExpenseReimbursement), StringComparison.OrdinalIgnoreCase);

    public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
    {
        if (action.Type != WorkflowActionType.SetField || !action.Field.Equals(nameof(OaExpenseReimbursement.Status), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"报销流程不支持动作：{action.Type}/{action.Field}/{action.Value}。");
        var item = repository.Get(context.Instance.BusinessId) ?? throw new InvalidOperationException("流程关联的报销单不存在或已被删除。");
        switch (action.Value)
        {
            case nameof(OaExpenseReimbursementStatus.Approved):
                if (workflowApprover is not null) workflowApprover.ApplyApproval(item); else { item.Approve(); repository.Update(item); }
                outcomeNotifications?.Publish(item.ApplicantUserId, nameof(OaExpenseReimbursement), item.Id, "报销申请", item.DocumentNo, "/Oa/ExpenseReimbursement", context.Instance.Id, "Approved");
                break;
            case nameof(OaExpenseReimbursementStatus.Rejected):
                if (workflowApprover is not null) workflowApprover.ApplyRejection(item, context.Reason); else { item.Reject(context.Reason); repository.Update(item); }
                outcomeNotifications?.Publish(item.ApplicantUserId, nameof(OaExpenseReimbursement), item.Id, "报销申请", item.DocumentNo, "/Oa/ExpenseReimbursement", context.Instance.Id, "Rejected", context.Reason);
                break;
            default:
                throw new InvalidOperationException($"报销流程不支持状态回写：{action.Value}。");
        }
    }
}
