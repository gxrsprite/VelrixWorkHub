using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.SalesOrders;

/// <summary>
/// Workflow 完成审批后推进销售订单的唯一应用层入口。
/// </summary>
public interface ISalesOrderWorkflowApprover
{
    void ApplyApproval(SalesOrder item);
}
