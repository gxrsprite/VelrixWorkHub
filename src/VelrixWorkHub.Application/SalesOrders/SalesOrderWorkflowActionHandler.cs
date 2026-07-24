using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.SalesOrders;

public sealed class SalesOrderWorkflowActionHandler(ISalesOrderRepository repository, ISalesOrderWorkflowApprover? workflowApprover = null) : IWorkflowActionHandler
{
    public bool CanHandle(string businessType) => businessType.Equals(nameof(SalesOrder), StringComparison.OrdinalIgnoreCase);

    public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
    {
        if (action.Type != WorkflowActionType.SetField || !action.Field.Equals(nameof(SalesOrder.Status), StringComparison.OrdinalIgnoreCase) || action.Value != nameof(SalesOrderStatus.Submitted))
            throw new InvalidOperationException($"销售订单流程不支持动作：{action.Type}/{action.Field}/{action.Value}。");

        var item = repository.List().FirstOrDefault(x => x.Id == context.Instance.BusinessId) ?? throw new InvalidOperationException("流程关联的销售订单不存在或已被删除。");
        if (workflowApprover is not null) workflowApprover.ApplyApproval(item);
        else
        {
            if (item.Status == SalesOrderStatus.Submitted) return;
            item.SetStatus(SalesOrderStatus.Submitted);
            repository.Update(item);
        }
    }
}
