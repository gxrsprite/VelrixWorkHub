using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Contracts;

/// <summary>
/// Workflow 完成合同审批后的唯一应用层推进入口。
/// </summary>
public interface ISalesContractWorkflowApprover
{
    void ApplyApproval(SalesContract item);
}
