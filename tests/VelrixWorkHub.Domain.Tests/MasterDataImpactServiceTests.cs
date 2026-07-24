using VelrixWorkHub.Application.MasterData;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class MasterDataImpactServiceTests
{
    [Fact]
    public void Product_ReportsOrderAndInventoryReferencesAndBalance()
    {
        var product = new Product("SKU-IMPACT", "影响检查商品", "件", 100m, null);
        var customer = new Customer("影响检查客户");
        var supplier = new Supplier("SUP-IMPACT", "影响检查供应商", null, null, null);
        var purchase = new PurchaseOrder("PO-IMPACT", supplier.Id, product.Id, DateOnly.FromDateTime(DateTime.Today), 5m, 10m);
        var sales = new SalesOrder("SO-IMPACT", customer.Id, product.Id, DateOnly.FromDateTime(DateTime.Today), 2m, 20m);
        var warehouse = new Warehouse("WH-IMPACT", "影响检查仓", null);
        var inbound = new InventoryTransaction(product.Id, warehouse.Id, InventoryTransactionKind.Inbound, 5m, "IN-IMPACT", DateOnly.FromDateTime(DateTime.Today), null);
        var outbound = new InventoryTransaction(product.Id, warehouse.Id, InventoryTransactionKind.Outbound, 2m, "OUT-IMPACT", DateOnly.FromDateTime(DateTime.Today), null);

        var result = MasterDataImpactService.Product(product.Id, [purchase], [sales], [inbound, outbound]);

        Assert.Equal(1, result.PurchaseOrderReferenceCount);
        Assert.Equal(1, result.SalesOrderReferenceCount);
        Assert.Equal(2, result.InventoryTransactionReferenceCount);
        Assert.Equal(3m, result.OnHandQuantity);
        Assert.True(result.HasReferences);

        var decision = MasterDataImpactService.Decide(result);

        Assert.False(decision.CanDelete);
        Assert.Equal("请停用商品并保留历史数据。", decision.SuggestedAction);
        Assert.Contains("采购订单 1 条", decision.ImpactObjects);
    }

    [Fact]
    public void Warehouse_OnlyCountsTransactionsBelongingToTheWarehouse()
    {
        var product = new Product("SKU-WH-IMPACT", "仓库影响商品", "件", 100m, null);
        var first = new Warehouse("WH-IMPACT-01", "一号仓", null);
        var second = new Warehouse("WH-IMPACT-02", "二号仓", null);
        var firstInbound = new InventoryTransaction(product.Id, first.Id, InventoryTransactionKind.Inbound, 8m, "IN-WH-01", DateOnly.FromDateTime(DateTime.Today), null);
        var secondInbound = new InventoryTransaction(product.Id, second.Id, InventoryTransactionKind.Inbound, 20m, "IN-WH-02", DateOnly.FromDateTime(DateTime.Today), null);

        var result = MasterDataImpactService.Warehouse(first.Id, [firstInbound, secondInbound]);

        Assert.Equal(1, result.InventoryTransactionReferenceCount);
        Assert.Equal(8m, result.OnHandQuantity);
        Assert.True(result.HasReferences);
    }

    [Fact]
    public void Customer_ReportsCrmErpAndPmpReferences()
    {
        var customer = new Customer("客户影响");
        var product = new Product("SKU-CUSTOMER-IMPACT", "客户影响商品", "件", 10m, null);
        var supplier = new Supplier("SUP-CUSTOMER-IMPACT", "客户影响供应商", null, null, null);
        var contact = new CustomerContact(customer.Id, "影响联系人");
        var followUp = new CustomerFollowUp(customer.Id, contact.Id, FollowUpType.Phone, "记录一次客户跟进", null);
        var contract = new SalesContract(customer.Id, null, "CTR-CUSTOMER-IMPACT", "客户影响合同", 100m, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(30)));
        var project = new PmpProject("PMP-CUSTOMER-IMPACT", "客户影响项目", customer.Id, null, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(30)));
        var order = new SalesOrder("SO-CUSTOMER-IMPACT", customer.Id, product.Id, DateOnly.FromDateTime(DateTime.Today), 1m, 100m, contract.Id, project.Id);
        var settlement = new ErpSettlement("REC-CUSTOMER-IMPACT", order.Id, customer.Id, ErpSettlementKind.Receivable, 50m, DateOnly.FromDateTime(DateTime.Today));
        var unrelatedOrder = new PurchaseOrder("PO-CUSTOMER-IMPACT", supplier.Id, product.Id, DateOnly.FromDateTime(DateTime.Today), 1m, 10m);

        var result = MasterDataImpactService.Customer(customer.Id, [contact], [followUp], [contract], [order], [project], [settlement, new ErpSettlement("PAY-CUSTOMER-IMPACT", unrelatedOrder.Id, supplier.Id, ErpSettlementKind.Payable, 10m, DateOnly.FromDateTime(DateTime.Today))]);

        Assert.Equal(1, result.ContactReferenceCount);
        Assert.Equal(1, result.FollowUpReferenceCount);
        Assert.Equal(1, result.ContractReferenceCount);
        Assert.Equal(1, result.SalesOrderReferenceCount);
        Assert.Equal(1, result.ProjectReferenceCount);
        Assert.Equal(1, result.SettlementReferenceCount);
        Assert.True(result.HasReferences);
    }

    [Fact]
    public void Supplier_ReportsPurchaseAndSettlementReferences()
    {
        var supplier = new Supplier("SUP-SUPPLIER-IMPACT", "供应商影响", null, null, null);
        var product = new Product("SKU-SUPPLIER-IMPACT", "供应商影响商品", "件", 10m, null);
        var purchase = new PurchaseOrder("PO-SUPPLIER-IMPACT", supplier.Id, product.Id, DateOnly.FromDateTime(DateTime.Today), 2m, 10m);
        var settlement = new ErpSettlement("PAY-SUPPLIER-IMPACT", purchase.Id, supplier.Id, ErpSettlementKind.Payable, 10m, DateOnly.FromDateTime(DateTime.Today));

        var result = MasterDataImpactService.Supplier(supplier.Id, [purchase], [settlement]);

        Assert.Equal(1, result.PurchaseOrderReferenceCount);
        Assert.Equal(1, result.SettlementReferenceCount);
        Assert.True(result.HasReferences);
    }
}
