using VelrixWorkHub.Application.Reports;

namespace VelrixWorkHub.Domain.Tests;

public sealed class CustomerErpInsightTests
{
    [Fact]
    public void Build_AggregatesValidSalesAndReceiptsForCustomer()
    {
        var customer = Guid.NewGuid(); var product = Guid.NewGuid();
        var shipped = SalesOrder.Restore(Guid.NewGuid(), "SO-CRM-01", customer, product, DateOnly.FromDateTime(DateTime.Today), 2, 100m, SalesOrderStatus.Shipped);
        var cancelled = SalesOrder.Restore(Guid.NewGuid(), "SO-CRM-02", customer, product, DateOnly.FromDateTime(DateTime.Today), 1, 50m, SalesOrderStatus.Cancelled);
        var other = new SalesOrder("SO-CRM-03", Guid.NewGuid(), product, DateOnly.FromDateTime(DateTime.Today), 1, 300m);
        var receipt = new ErpSettlement("REC-CRM-01", shipped.Id, customer, ErpSettlementKind.Receivable, 80m, DateOnly.FromDateTime(DateTime.Today));

        var result = CustomerErpInsightService.Build(customer, [shipped, cancelled, other], [receipt]);

        Assert.Equal(1, result.OrderCount); Assert.Equal(1, result.ShippedOrderCount); Assert.Equal(200m, result.SalesAmount); Assert.Equal(80m, result.ReceivedAmount); Assert.Equal(120m, result.ReceivableAmount);
    }

    [Fact]
    public void Build_ExcludesVoidedReceiptsFromCustomerBalance()
    {
        var customer = Guid.NewGuid(); var product = Guid.NewGuid();
        var order = SalesOrder.Restore(Guid.NewGuid(), "SO-CRM-VOID-01", customer, product, DateOnly.FromDateTime(DateTime.Today), 1, 100m, SalesOrderStatus.Shipped);
        var active = new ErpSettlement("REC-CRM-ACTIVE-01", order.Id, customer, ErpSettlementKind.Receivable, 30m, DateOnly.FromDateTime(DateTime.Today));
        var voided = new ErpSettlement("REC-CRM-VOID-01", order.Id, customer, ErpSettlementKind.Receivable, 70m, DateOnly.FromDateTime(DateTime.Today));
        voided.Void("回单录入错误");

        var result = CustomerErpInsightService.Build(customer, [order], [active, voided]);

        Assert.Equal(30m, result.ReceivedAmount); Assert.Equal(70m, result.ReceivableAmount);
    }
}
