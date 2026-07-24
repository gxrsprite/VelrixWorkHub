using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Reports;

public sealed record ContractOrderInsight(Guid ContractId, int OrderCount, int ShippedOrderCount, decimal OrderAmount, decimal ContractAmount = 0)
{
    public decimal RemainingAmount => decimal.Round(Math.Max(ContractAmount - OrderAmount, 0), 2);
}

public static class ContractOrderInsightService
{
    public static ContractOrderInsight Build(Guid contractId, IEnumerable<SalesOrder> orders)
    {
        var related = orders.Where(x => x.ContractId == contractId && x.Status != SalesOrderStatus.Cancelled).ToArray();
        return new ContractOrderInsight(contractId, related.Length, related.Count(x => x.Status == SalesOrderStatus.Shipped), related.Sum(x => x.Amount));
    }

    public static ContractOrderInsight Build(SalesContract contract, IEnumerable<SalesOrder> orders)
    {
        var result = Build(contract.Id, orders);
        return result with { ContractAmount = contract.Amount };
    }
}
