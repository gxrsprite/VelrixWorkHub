using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Assets;

public sealed class AssetRequestWorkflowActionHandler(
    IOaAssetRequestRepository repository,
    IOaAssetRequestWorkflowApprover? workflowApprover = null,
    OaWorkflowOutcomeNotificationService? outcomeNotifications = null) : IWorkflowActionHandler
{
    public bool CanHandle(string businessType) => businessType.Equals(nameof(OaAssetRequest), StringComparison.OrdinalIgnoreCase);

    public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
    {
        if (action.Type != WorkflowActionType.SetField || !action.Field.Equals(nameof(OaAssetRequest.Status), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"资产申请流程不支持动作：{action.Type}/{action.Field}/{action.Value}。");

        var request = repository.Get(context.Instance.BusinessId) ?? throw new InvalidOperationException("流程关联的资产申请不存在或已被删除。");
        switch (action.Value)
        {
            case nameof(OaAssetRequestStatus.Approved):
                if (workflowApprover is not null) workflowApprover.ApplyApproval(request, context.Actor);
                else throw new InvalidOperationException("资产申请审批服务未配置，不能锁定资产。");
                outcomeNotifications?.Publish(request.ApplicantUserId, nameof(OaAssetRequest), request.Id, "资产申请",
                    request.Reason, "/Oa/Asset", context.Instance.Id, "Approved");
                break;
            case nameof(OaAssetRequestStatus.Rejected):
                if (workflowApprover is not null) workflowApprover.ApplyRejection(request, context.Reason);
                else { request.Reject(context.Reason); repository.Update(request); }
                outcomeNotifications?.Publish(request.ApplicantUserId, nameof(OaAssetRequest), request.Id, "资产申请",
                    request.Reason, "/Oa/Asset", context.Instance.Id, "Rejected", context.Reason);
                break;
            default:
                throw new InvalidOperationException($"资产申请流程不支持状态回写：{action.Value}。");
        }
    }
}
