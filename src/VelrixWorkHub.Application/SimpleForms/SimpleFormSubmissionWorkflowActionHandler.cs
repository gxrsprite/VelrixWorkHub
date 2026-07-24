using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.SimpleForms;

public sealed class SimpleFormSubmissionWorkflowActionHandler(
    ISimpleFormSubmissionRepository repository,
    ISimpleFormSubmissionWorkflowApprover? workflowApprover = null) : IWorkflowActionHandler
{
    public bool CanHandle(string businessType) => businessType.Equals(nameof(SimpleFormSubmission), StringComparison.OrdinalIgnoreCase);
    public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
    {
        if (action.Type != WorkflowActionType.SetField || !action.Field.Equals(nameof(SimpleFormSubmission.Status), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"简单表单流程不支持动作：{action.Type}/{action.Field}/{action.Value}。");
        var item = repository.Get(context.Instance.BusinessId) ?? throw new InvalidOperationException("流程关联的表单申请不存在或已被删除。");
        switch (action.Value)
        {
            case nameof(SimpleFormSubmissionStatus.Approved): if (workflowApprover is not null) workflowApprover.ApplyApproval(item); else { item.Approve(); repository.Update(item); } break;
            case nameof(SimpleFormSubmissionStatus.Rejected): if (workflowApprover is not null) workflowApprover.ApplyRejection(item, context.Reason); else { item.Reject(context.Reason); repository.Update(item); } break;
            default: throw new InvalidOperationException($"简单表单流程不支持状态回写：{action.Value}。");
        }
    }
}
