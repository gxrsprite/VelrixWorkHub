using VelrixWorkHub.Application.Reports;

namespace VelrixWorkHub.Domain.Tests;

public sealed class CustomerContractInsightTests
{
    [Fact]
    public void Build_ReportsActiveContractAmountAndUnorderedBalance()
    {
        var customer = Guid.NewGuid(); var product = Guid.NewGuid(); var today = DateOnly.FromDateTime(DateTime.Today);
        var active = new SalesContract(customer, null, "CT-INSIGHT-01", "有效合同", 1000m, today, today.AddYears(1)); active.Activate();
        var draft = new SalesContract(customer, null, "CT-INSIGHT-02", "草稿合同", 500m, today, today.AddYears(1));
        var terminated = new SalesContract(customer, null, "CT-INSIGHT-03", "终止合同", 300m, today.AddYears(-2), today.AddYears(-1)); terminated.Activate(); terminated.Terminate();
        var linkedOrder = SalesOrder.Restore(Guid.NewGuid(), "SO-INSIGHT-01", customer, product, today, 2, 200m, SalesOrderStatus.Shipped, active.Id);
        var cancelledLinkedOrder = SalesOrder.Restore(Guid.NewGuid(), "SO-INSIGHT-02", customer, product, today, 1, 100m, SalesOrderStatus.Cancelled, active.Id);
        var draftLinkedOrder = SalesOrder.Restore(Guid.NewGuid(), "SO-INSIGHT-03", customer, product, today, 1, 50m, SalesOrderStatus.Submitted, draft.Id);

        var result = CustomerContractInsightService.Build(customer, [active, draft, terminated], [linkedOrder, cancelledLinkedOrder, draftLinkedOrder]);

        Assert.Equal(1, result.ActiveContractCount);
        Assert.Equal(1000m, result.ActiveContractAmount);
        Assert.Equal(400m, result.ContractedOrderAmount);
        Assert.Equal(600m, result.UnorderedContractAmount);
    }
}
