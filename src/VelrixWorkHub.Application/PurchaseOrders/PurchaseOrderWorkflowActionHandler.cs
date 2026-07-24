using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PurchaseOrders;

public sealed class PurchaseOrderWorkflowActionHandler(IPurchaseOrderRepository repository, IPurchaseOrderWorkflowApprover? workflowApprover = null) : IWorkflowActionHandler
{
    public bool CanHandle(string businessType) => businessType.Equals(nameof(PurchaseOrder), StringComparison.OrdinalIgnoreCase);

    public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
    {
        if (action.Type != WorkflowActionType.SetField || !action.Field.Equals(nameof(PurchaseOrder.Status), StringComparison.OrdinalIgnoreCase) || action.Value != nameof(PurchaseOrderStatus.Submitted))
            throw new InvalidOperationException($"采购订单流程不支持动作：{action.Type}/{action.Field}/{action.Value}。");

        var item = repository.List().FirstOrDefault(x => x.Id == context.Instance.BusinessId) ?? throw new InvalidOperationException("流程关联的采购订单不存在或已被删除。");
        if (workflowApprover is not null) workflowApprover.ApplyApproval(item);
        else
        {
            if (item.Status == PurchaseOrderStatus.Submitted) return;
            item.SetStatus(PurchaseOrderStatus.Submitted);
            repository.Update(item);
        }
    }
}
