using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Vehicles;

public sealed class VehicleUseRequestWorkflowActionHandler(
    IOaVehicleUseRequestRepository repository,
    IOaVehicleUseWorkflowApprover? workflowApprover = null) : IWorkflowActionHandler
{
    public bool CanHandle(string businessType) => businessType.Equals(nameof(OaVehicleUseRequest), StringComparison.OrdinalIgnoreCase);

    public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
    {
        if (action.Type != WorkflowActionType.SetField || !action.Field.Equals(nameof(OaVehicleUseRequest.Status), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"用车申请流程不支持动作：{action.Type}/{action.Field}/{action.Value}。");
        var request = repository.Get(context.Instance.BusinessId) ?? throw new InvalidOperationException("流程关联的用车申请不存在或已被删除。");
        switch (action.Value)
        {
            case nameof(OaVehicleUseRequestStatus.Approved):
                if (workflowApprover is not null) workflowApprover.ApplyApproval(request);
                else { request.Approve(); repository.Update(request); }
                break;
            case nameof(OaVehicleUseRequestStatus.Rejected):
                if (workflowApprover is not null) workflowApprover.ApplyRejection(request, context.Reason);
                else { request.Reject(context.Reason); repository.Update(request); }
                break;
            default:
                throw new InvalidOperationException($"用车申请流程不支持状态回写：{action.Value}。");
        }
    }
}
