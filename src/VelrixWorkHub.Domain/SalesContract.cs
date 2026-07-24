namespace VelrixWorkHub.Domain;
public enum ContractStatus { Draft, Active, Terminated }
public sealed class SalesContract
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid CustomerId { get; private set; }
    public Guid? OpportunityId { get; private set; }
    public string ContractNo { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public ContractStatus Status { get; private set; }
    public SalesContract(Guid customerId, Guid? opportunityId, string contractNo, string title, decimal amount, DateOnly startDate, DateOnly endDate)
    { Edit(customerId, opportunityId, contractNo, title, amount, startDate, endDate); Status = ContractStatus.Draft; }
    public void Edit(Guid customerId, Guid? opportunityId, string contractNo, string title, decimal amount, DateOnly startDate, DateOnly endDate)
    {
        if (customerId == Guid.Empty) throw new ArgumentException("必须选择客户。", nameof(customerId));
        if (string.IsNullOrWhiteSpace(contractNo)) throw new ArgumentException("合同编号不能为空。", nameof(contractNo));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("合同名称不能为空。", nameof(title));
        if (amount < 0) throw new ArgumentException("合同金额不能为负数。", nameof(amount));
        if (endDate < startDate) throw new ArgumentException("结束日期不能早于开始日期。", nameof(endDate));
        CustomerId = customerId; OpportunityId = opportunityId; ContractNo = contractNo.Trim(); Title = title.Trim(); Amount = amount; StartDate = startDate; EndDate = endDate;
    }
    public void Activate() { if (Status == ContractStatus.Terminated) throw new InvalidOperationException("已终止合同不能重新生效。"); Status = ContractStatus.Active; }
    public void Terminate() { if (Status != ContractStatus.Active) throw new InvalidOperationException("只有生效合同可以终止。"); Status = ContractStatus.Terminated; }
}
