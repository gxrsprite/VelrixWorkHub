using VelrixWorkHub.Domain;
using VelrixWorkHub.Application.Workflow;
namespace VelrixWorkHub.Application.Contracts;
public sealed class SalesContractService(ISalesContractRepository repository, WorkflowApprovalService? approval = null) : ISalesContractWorkflowApprover
{
    public IReadOnlyList<SalesContract> List(string? keyword = null, ContractFilter filter = ContractFilter.All)
    {
        var query = repository.List().AsEnumerable(); var text = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(text)) query = query.Where(item => item.ContractNo.Contains(text, StringComparison.OrdinalIgnoreCase) || item.Title.Contains(text, StringComparison.OrdinalIgnoreCase));
        query = filter switch { ContractFilter.Draft => query.Where(item => item.Status == ContractStatus.Draft), ContractFilter.Active => query.Where(item => item.Status == ContractStatus.Active), ContractFilter.Terminated => query.Where(item => item.Status == ContractStatus.Terminated), _ => query };
        return query.ToArray();
    }
    public int Count(ContractFilter filter) => List(filter: filter).Count;
    public SalesContract Create(Guid customerId, Guid? opportunityId, string no, string title, decimal amount, DateOnly start, DateOnly end) { var item = new SalesContract(customerId, opportunityId, no, title, amount, start, end); repository.Add(item); return item; }
    public void Edit(SalesContract item, Guid customerId, Guid? opportunityId, string no, string title, decimal amount, DateOnly start, DateOnly end)
    {
        if (item.Status == ContractStatus.Active) throw new InvalidOperationException("生效合同不能直接编辑，请终止后新建变更合同。");
        if (approval?.Latest(WorkflowBindingCodes.ContractApproval, nameof(SalesContract), item.Id)?.Status == WorkflowInstanceStatus.Completed)
            throw new InvalidOperationException("合同审批已完成，不能修改已审批内容；请重新创建合同变更。");
        approval?.RequireNotRunning(WorkflowBindingCodes.ContractApproval, nameof(SalesContract), item.Id, "编辑合同");
        item.Edit(customerId, opportunityId, no, title, amount, start, end);
        repository.Update(item);
    }
    public void Activate(SalesContract item) { approval?.RequireCompleted(WorkflowBindingCodes.ContractApproval, nameof(SalesContract), item.Id, "合同生效"); item.Activate(); repository.Update(item); }
    public void ApplyApproval(SalesContract item)
    {
        if (item.Status == ContractStatus.Active) return;
        if (item.Status != ContractStatus.Draft) throw new InvalidOperationException($"合同不能从“{item.Status}”通过审批。");
        item.Activate();
        repository.Update(item);
    }
    public void Terminate(SalesContract item) { item.Terminate(); repository.Update(item); }
    public void Remove(SalesContract item) { approval?.RequireNotRunning(WorkflowBindingCodes.ContractApproval, nameof(SalesContract), item.Id, "删除合同"); repository.Remove(item.Id); }
}
