using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PurchaseOrders;

/// <summary>
/// Workflow 完成审批后推进采购订单的唯一应用层入口。
/// </summary>
public interface IPurchaseOrderWorkflowApprover
{
    void ApplyApproval(PurchaseOrder item);
}
