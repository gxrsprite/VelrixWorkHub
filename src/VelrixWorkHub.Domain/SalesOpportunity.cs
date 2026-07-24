namespace VelrixWorkHub.Domain;
public enum OpportunityStage { Prospecting, Qualified, Proposal, Negotiation, Won, Lost }
public sealed class SalesOpportunity
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid CustomerId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public OpportunityStage Stage { get; private set; }
    public decimal? ExpectedAmount { get; private set; }
    public DateOnly? ExpectedCloseDate { get; private set; }
    public string? LostReason { get; private set; }
    public SalesOpportunity(Guid customerId, string title, decimal? amount = null, DateOnly? closeDate = null) { Edit(customerId, title, amount, closeDate); Stage = OpportunityStage.Prospecting; }
    public void Edit(Guid customerId, string title, decimal? amount, DateOnly? closeDate)
    {
        if (customerId == Guid.Empty) throw new ArgumentException("必须选择客户。", nameof(customerId));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("商机名称不能为空。", nameof(title));
        if (amount is < 0) throw new ArgumentException("预计金额不能为负数。", nameof(amount));
        CustomerId = customerId; Title = title.Trim(); ExpectedAmount = amount; ExpectedCloseDate = closeDate;
    }
    public void MoveTo(OpportunityStage stage, string? lostReason = null) { if (stage == OpportunityStage.Lost && string.IsNullOrWhiteSpace(lostReason)) throw new ArgumentException("输单时必须填写原因。", nameof(lostReason)); Stage = stage; LostReason = stage == OpportunityStage.Lost ? lostReason!.Trim() : null; }
}
