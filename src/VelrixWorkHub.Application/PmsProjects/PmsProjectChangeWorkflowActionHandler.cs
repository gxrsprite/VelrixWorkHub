using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmsProjects;

public sealed class PmsProjectChangeWorkflowActionHandler(IPmsProjectChangeRepository repository, IPmsProjectChangeWorkflowApprover? workflowApprover = null) : IWorkflowActionHandler
{
    public bool CanHandle(string businessType) => businessType.Equals(nameof(PmsProjectChange), StringComparison.OrdinalIgnoreCase);

    public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
    {
        if (action.Type != WorkflowActionType.SetField || !action.Field.Equals(nameof(PmsProjectChange.Status), StringComparison.OrdinalIgnoreCase) || action.Value != nameof(PmsProjectChangeStatus.Approved))
            throw new InvalidOperationException($"项目变更流程不支持动作：{action.Type}/{action.Field}/{action.Value}。");

        var item = repository.List().FirstOrDefault(x => x.Id == context.Instance.BusinessId) ?? throw new InvalidOperationException("流程关联的项目变更不存在或已被删除。");
        if (workflowApprover is not null) workflowApprover.ApplyApproval(item);
        else
        {
            if (item.Status == PmsProjectChangeStatus.Approved) return;
            if (item.Status != PmsProjectChangeStatus.Proposed) throw new InvalidOperationException($"项目变更不能从“{item.Status}”通过审批。");
            item.SetStatus(PmsProjectChangeStatus.Approved);
            repository.Update(item);
        }
    }
}
