using VelrixWorkHub.Application.Reports;

namespace VelrixWorkHub.Domain.Tests;

public sealed class ContractOrderInsightTests
{
    [Fact]
    public void Build_UsesOnlyNonCancelledOrdersForSelectedContract()
    {
        var contract = Guid.NewGuid(); var customer = Guid.NewGuid(); var product = Guid.NewGuid();
        var shipped = SalesOrder.Restore(Guid.NewGuid(), "SO-CT-01", customer, product, DateOnly.FromDateTime(DateTime.Today), 2, 100m, SalesOrderStatus.Shipped, contract);
        var cancelled = SalesOrder.Restore(Guid.NewGuid(), "SO-CT-02", customer, product, DateOnly.FromDateTime(DateTime.Today), 1, 50m, SalesOrderStatus.Cancelled, contract);
        var other = new SalesOrder("SO-CT-03", customer, product, DateOnly.FromDateTime(DateTime.Today), 1, 300m);

        var result = ContractOrderInsightService.Build(contract, [shipped, cancelled, other]);

        Assert.Equal(1, result.OrderCount); Assert.Equal(1, result.ShippedOrderCount); Assert.Equal(200m, result.OrderAmount);
    }

    [Fact]
    public void Build_WithContract_ReportsRemainingContractAmount()
    {
        var customer = Guid.NewGuid(); var product = Guid.NewGuid(); var today = DateOnly.FromDateTime(DateTime.Today);
        var contract = new SalesContract(customer, null, "CT-REMAINING-01", "履约合同", 1000m, today, today.AddYears(1));
        var order = SalesOrder.Restore(Guid.NewGuid(), "SO-REMAINING-01", customer, product, today, 2, 100m, SalesOrderStatus.Submitted, contract.Id);

        var result = ContractOrderInsightService.Build(contract, [order]);

        Assert.Equal(1000m, result.ContractAmount);
        Assert.Equal(200m, result.OrderAmount);
        Assert.Equal(800m, result.RemainingAmount);
    }

    [Fact]
    public void Build_WithOverFulfilledContract_ClampsRemainingAmountToZero()
    {
        var customer = Guid.NewGuid(); var product = Guid.NewGuid(); var today = DateOnly.FromDateTime(DateTime.Today);
        var contract = new SalesContract(customer, null, "CT-REMAINING-02", "超额履约合同", 100m, today, today.AddYears(1));
        var order = SalesOrder.Restore(Guid.NewGuid(), "SO-REMAINING-02", customer, product, today, 2, 100m, SalesOrderStatus.Shipped, contract.Id);

        var result = ContractOrderInsightService.Build(contract, [order]);

        Assert.Equal(0m, result.RemainingAmount);
    }
}
