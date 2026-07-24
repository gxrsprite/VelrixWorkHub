using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmpProjects;

public sealed class PmpProjectChangeWorkflowActionHandler(IPmpProjectChangeRepository repository, IPmpProjectChangeWorkflowApprover? workflowApprover = null) : IWorkflowActionHandler
{
    public bool CanHandle(string businessType) => businessType.Equals(nameof(PmpProjectChange), StringComparison.OrdinalIgnoreCase);

    public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
    {
        if (action.Type != WorkflowActionType.SetField || !action.Field.Equals(nameof(PmpProjectChange.Status), StringComparison.OrdinalIgnoreCase) || action.Value != nameof(PmpProjectChangeStatus.Approved))
            throw new InvalidOperationException($"项目变更流程不支持动作：{action.Type}/{action.Field}/{action.Value}。");

        var item = repository.List().FirstOrDefault(x => x.Id == context.Instance.BusinessId) ?? throw new InvalidOperationException("流程关联的项目变更不存在或已被删除。");
        if (workflowApprover is not null) workflowApprover.ApplyApproval(item);
        else
        {
            if (item.Status == PmpProjectChangeStatus.Approved) return;
            if (item.Status != PmpProjectChangeStatus.Proposed) throw new InvalidOperationException($"项目变更不能从“{item.Status}”通过审批。");
            item.SetStatus(PmpProjectChangeStatus.Approved);
            repository.Update(item);
        }
    }
}
