using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmsProjects;

public sealed class PmsProjectWorkItemWorkflowActionHandler(
    IPmsProjectWorkItemRepository repository,
    IPmsProjectWorkItemWorkflowApprover? workflowApprover = null) : IWorkflowActionHandler
{
    public bool CanHandle(string businessType) => businessType.Equals(nameof(PmsProjectWorkItem), StringComparison.OrdinalIgnoreCase);

    public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
    {
        if (action.Type != WorkflowActionType.SetField || !action.Field.Equals(nameof(PmsProjectWorkItem.Status), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"工作项验收流程不支持动作：{action.Type}/{action.Field}/{action.Value}。");
        var item = repository.List().FirstOrDefault(x => x.Id == context.Instance.BusinessId) ?? throw new InvalidOperationException("流程关联的工作项不存在或已被删除。");
        switch (action.Value)
        {
            case nameof(PmsProjectWorkItemStatus.Completed):
                if (workflowApprover is not null) workflowApprover.ApplyCompletionApproval(item);
                else { item.ApproveCompletion(DateTime.Now); repository.Update(item); }
                break;
            case nameof(PmsProjectWorkItemStatus.InProgress):
                if (workflowApprover is not null) workflowApprover.ApplyCompletionRejection(item, context.Reason);
                else { item.RejectCompletion(context.Reason); repository.Update(item); }
                break;
            default:
                throw new InvalidOperationException($"工作项验收流程不支持状态回写：{action.Value}。");
        }
    }
}
