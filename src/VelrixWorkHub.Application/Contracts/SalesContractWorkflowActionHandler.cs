using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Contracts;

public sealed class SalesContractWorkflowActionHandler(ISalesContractRepository repository, ISalesContractWorkflowApprover? workflowApprover = null) : IWorkflowActionHandler
{
    public bool CanHandle(string businessType) => businessType.Equals(nameof(SalesContract), StringComparison.OrdinalIgnoreCase);

    public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
    {
        if (action.Type != WorkflowActionType.SetField || !action.Field.Equals(nameof(SalesContract.Status), StringComparison.OrdinalIgnoreCase) || action.Value != nameof(ContractStatus.Active))
            throw new InvalidOperationException($"合同流程不支持动作：{action.Type}/{action.Field}/{action.Value}。");

        var item = repository.List().FirstOrDefault(x => x.Id == context.Instance.BusinessId) ?? throw new InvalidOperationException("流程关联的合同不存在或已被删除。");
        if (workflowApprover is not null) workflowApprover.ApplyApproval(item);
        else
        {
            if (item.Status == ContractStatus.Active) return;
            item.Activate();
            repository.Update(item);
        }
    }
}
