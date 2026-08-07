using VelrixWorkHub.Application.PmsProjects;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PmsProjectCommercialInsightTests
{
    [Fact]
    public void Build_UsesProjectOrdersAndTheirSettlementsOnly()
    {
        var today = DateOnly.FromDateTime(DateTime.Today); var customer = new Customer("Aster 科技"); var product = new Product("SKU-COMMERCIAL", "项目服务", "件", 100m, null);
        var project = new PmsProject("PRJ-COMMERCIAL-01", "项目一", customer.Id, null, today, today.AddDays(30));
        var otherProject = new PmsProject("PRJ-COMMERCIAL-02", "项目二", customer.Id, null, today, today.AddDays(30));
        var contract = new SalesContract(customer.Id, null, "CT-COMMERCIAL-01", "项目合同", 500m, today, today.AddDays(30)); contract.Activate();
        var projectOrder = new SalesOrder("SO-COMMERCIAL-01", customer.Id, product.Id, today, 3, 100m, contract.Id, project.Id);
        var otherOrder = new SalesOrder("SO-COMMERCIAL-02", customer.Id, product.Id, today, 1, 100m, contract.Id, otherProject.Id);
        var projectReceipt = new ErpSettlement("REC-COMMERCIAL-01", projectOrder.Id, customer.Id, ErpSettlementKind.Receivable, 50m, today);
        var otherReceipt = new ErpSettlement("REC-COMMERCIAL-02", otherOrder.Id, customer.Id, ErpSettlementKind.Receivable, 100m, today);

        var result = PmsProjectCommercialInsightService.Build(project, [projectOrder, otherOrder], [projectReceipt, otherReceipt], [contract]);

        Assert.True(result.HasProjectScopedOrders);
        Assert.Equal(1, result.SalesOrderCount);
        Assert.Equal(300m, result.SalesOrderAmount);
        Assert.Equal(3m, result.SalesOrderQuantity);
        Assert.Equal(0m, result.ShippedOrderQuantity);
        Assert.Equal(3m, result.PendingShipmentQuantity);
        Assert.Equal(50m, result.ReceivedAmount);
        Assert.Equal(250m, result.ReceivableAmount);
        Assert.Equal(500m, result.ActiveContractAmount);
        Assert.Equal(300m, result.ContractedOrderAmount);
        Assert.Equal(200m, result.UnorderedContractAmount);

        var details = PmsProjectCommercialInsightService.Orders(project, [projectOrder, otherOrder], [projectReceipt, otherReceipt]);

        Assert.Single(details);
        Assert.Equal(projectOrder.Id, details[0].Order.Id);
        Assert.Equal(50m, details[0].ReceivedAmount);
        Assert.Equal(250m, details[0].ReceivableAmount);
    }

    [Fact]
    public void Build_RecomputesAfterCancellationAndVoidedReceipt()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var customer = new Customer("履约重算客户");
        var product = new Product("SKU-COMMERCIAL-RECALC", "履约重算商品", "件", 100m, null);
        var project = new PmsProject("PRJ-COMMERCIAL-RECALC", "履约重算项目", customer.Id, null, today, today.AddDays(30));
        var shippedOrder = new SalesOrder("SO-COMMERCIAL-SHIPPED", customer.Id, product.Id, today, 2m, 100m, null, project.Id);
        shippedOrder.SetStatus(SalesOrderStatus.Submitted);
        shippedOrder.SetStatus(SalesOrderStatus.Shipped);
        var cancelledOrder = new SalesOrder("SO-COMMERCIAL-CANCELLED", customer.Id, product.Id, today, 5m, 100m, null, project.Id);
        cancelledOrder.SetStatus(SalesOrderStatus.Cancelled);
        var activeReceipt = new ErpSettlement("REC-COMMERCIAL-ACTIVE", shippedOrder.Id, customer.Id, ErpSettlementKind.Receivable, 50m, today);
        var voidedReceipt = new ErpSettlement("REC-COMMERCIAL-VOIDED", shippedOrder.Id, customer.Id, ErpSettlementKind.Receivable, 25m, today);
        voidedReceipt.Void("履约重算测试撤销");

        var result = PmsProjectCommercialInsightService.Build(project, [shippedOrder, cancelledOrder], [activeReceipt, voidedReceipt], []);

        Assert.Equal(1, result.SalesOrderCount);
        Assert.Equal(200m, result.SalesOrderAmount);
        Assert.Equal(2m, result.SalesOrderQuantity);
        Assert.Equal(200m, result.ShippedOrderAmount);
        Assert.Equal(2m, result.ShippedOrderQuantity);
        Assert.Equal(50m, result.ReceivedAmount);
        Assert.Equal(150m, result.ReceivableAmount);
    }
}
