using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Overtime;

public sealed class OvertimeRequestWorkflowActionHandler(
    IOaOvertimeRequestRepository repository,
    IOaOvertimeRequestWorkflowApprover? workflowApprover = null,
    OaWorkflowOutcomeNotificationService? outcomeNotifications = null) : IWorkflowActionHandler
{
    public bool CanHandle(string businessType) => businessType.Equals(nameof(OaOvertimeRequest), StringComparison.OrdinalIgnoreCase);

    public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
    {
        if (action.Type != WorkflowActionType.SetField || !action.Field.Equals(nameof(OaOvertimeRequest.Status), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"加班申请流程不支持动作：{action.Type}/{action.Field}/{action.Value}。");

        var item = repository.Get(context.Instance.BusinessId) ?? throw new InvalidOperationException("流程关联的加班申请不存在或已被删除。");
        switch (action.Value)
        {
            case nameof(OaOvertimeRequestStatus.Approved):
                if (workflowApprover is not null) workflowApprover.ApplyApproval(item);
                else { item.Approve(); repository.Update(item); }
                outcomeNotifications?.Publish(item.UserId, nameof(OaOvertimeRequest), item.Id, "加班申请",
                    $"{item.StartAt:yyyy-MM-dd HH:mm} 至 {item.EndAt:yyyy-MM-dd HH:mm}", "/Oa/Overtime", context.Instance.Id, "Approved");
                break;
            case nameof(OaOvertimeRequestStatus.Rejected):
                if (workflowApprover is not null) workflowApprover.ApplyRejection(item, context.Reason);
                else { item.Reject(context.Reason); repository.Update(item); }
                outcomeNotifications?.Publish(item.UserId, nameof(OaOvertimeRequest), item.Id, "加班申请",
                    $"{item.StartAt:yyyy-MM-dd HH:mm} 至 {item.EndAt:yyyy-MM-dd HH:mm}", "/Oa/Overtime", context.Instance.Id, "Rejected", context.Reason);
                break;
            default:
                throw new InvalidOperationException($"加班申请流程不支持状态回写：{action.Value}。");
        }
    }
}
