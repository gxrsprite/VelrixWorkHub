using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Leave;

public sealed class LeaveRequestWorkflowActionHandler(
    IOaLeaveRequestRepository repository,
    IOaLeaveRequestWorkflowApprover? workflowApprover = null,
    OaWorkflowOutcomeNotificationService? outcomeNotifications = null) : IWorkflowActionHandler
{
    public bool CanHandle(string businessType) => businessType.Equals(nameof(OaLeaveRequest), StringComparison.OrdinalIgnoreCase);

    public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
    {
        if (action.Type != WorkflowActionType.SetField || !action.Field.Equals(nameof(OaLeaveRequest.Status), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"请假申请流程不支持动作：{action.Type}/{action.Field}/{action.Value}。");

        var item = repository.Get(context.Instance.BusinessId) ?? throw new InvalidOperationException("流程关联的请假申请不存在或已被删除。");
        switch (action.Value)
        {
            case nameof(OaLeaveRequestStatus.Approved):
                if (workflowApprover is not null) workflowApprover.ApplyApproval(item);
                else { item.Approve(); repository.Update(item); }
                outcomeNotifications?.Publish(item.UserId, nameof(OaLeaveRequest), item.Id, "请假申请",
                    $"{item.StartAt:yyyy-MM-dd HH:mm} 至 {item.EndAt:yyyy-MM-dd HH:mm}", "/Oa/Leave", context.Instance.Id, "Approved");
                break;
            case nameof(OaLeaveRequestStatus.Rejected):
                if (workflowApprover is not null) workflowApprover.ApplyRejection(item, context.Reason);
                else { item.Reject(context.Reason); repository.Update(item); }
                outcomeNotifications?.Publish(item.UserId, nameof(OaLeaveRequest), item.Id, "请假申请",
                    $"{item.StartAt:yyyy-MM-dd HH:mm} 至 {item.EndAt:yyyy-MM-dd HH:mm}", "/Oa/Leave", context.Instance.Id, "Rejected", context.Reason);
                break;
            default:
                throw new InvalidOperationException($"请假申请流程不支持状态回写：{action.Value}。");
        }
    }
}
