using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Reports;

public sealed record CustomerContractInsight(Guid CustomerId, int ActiveContractCount, decimal ActiveContractAmount, decimal ContractedOrderAmount)
{
    public decimal UnorderedContractAmount => decimal.Round(Math.Max(ActiveContractAmount - ContractedOrderAmount, 0), 2);
}

public static class CustomerContractInsightService
{
    public static CustomerContractInsight Build(Guid customerId, IEnumerable<SalesContract> contracts, IEnumerable<SalesOrder> orders)
    {
        var activeContracts = contracts.Where(x => x.CustomerId == customerId && x.Status == ContractStatus.Active).ToArray();
        var activeContractIds = activeContracts.Select(x => x.Id).ToHashSet();
        var contractedOrderAmount = orders
            .Where(x => x.CustomerId == customerId && x.ContractId is Guid contractId && activeContractIds.Contains(contractId) && x.Status != SalesOrderStatus.Cancelled)
            .Sum(x => x.Amount);

        return new CustomerContractInsight(customerId, activeContracts.Length, activeContracts.Sum(x => x.Amount), contractedOrderAmount);
    }
}
