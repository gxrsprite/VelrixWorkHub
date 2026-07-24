using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PaymentRequests;

public sealed class PaymentRequestWorkflowActionHandler(
    IOaPaymentRequestRepository repository,
    IOaPaymentRequestWorkflowApprover? workflowApprover = null,
    OaWorkflowOutcomeNotificationService? outcomeNotifications = null) : IWorkflowActionHandler
{
    public bool CanHandle(string businessType) => businessType.Equals(nameof(OaPaymentRequest), StringComparison.OrdinalIgnoreCase);

    public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
    {
        if (action.Type != WorkflowActionType.SetField || !action.Field.Equals(nameof(OaPaymentRequest.Status), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"付款申请流程不支持动作：{action.Type}/{action.Field}/{action.Value}。");
        var item = repository.Get(context.Instance.BusinessId) ?? throw new InvalidOperationException("流程关联的付款申请不存在或已被删除。");
        switch (action.Value)
        {
            case nameof(OaPaymentRequestStatus.Approved):
                if (workflowApprover is not null) workflowApprover.ApplyApproval(item, context.Actor); else { item.Approve(); repository.Update(item); }
                outcomeNotifications?.Publish(item.ApplicantUserId, nameof(OaPaymentRequest), item.Id, "付款申请", item.DocumentNo, "/Oa/PaymentRequest", context.Instance.Id, "Approved");
                break;
            case nameof(OaPaymentRequestStatus.Rejected):
                if (workflowApprover is not null) workflowApprover.ApplyRejection(item, context.Reason, context.Actor); else { item.Reject(context.Reason); repository.Update(item); }
                outcomeNotifications?.Publish(item.ApplicantUserId, nameof(OaPaymentRequest), item.Id, "付款申请", item.DocumentNo, "/Oa/PaymentRequest", context.Instance.Id, "Rejected", context.Reason);
                break;
            default:
                throw new InvalidOperationException($"付款申请流程不支持状态回写：{action.Value}。");
        }
    }
}
