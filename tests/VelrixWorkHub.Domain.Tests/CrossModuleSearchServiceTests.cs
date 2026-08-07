using VelrixWorkHub.Application.Reports;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class CrossModuleSearchServiceTests
{
    [Fact]
    public void Build_SearchingCustomerReturnsRelatedBusinessObjects()
    {
        var customer = new Customer("Aster 科技", "林经理");
        var contract = new SalesContract(customer.Id, null, "CT-ASTER-01", "Aster 年度服务", 1000m, Today, Today.AddDays(30)); contract.Activate();
        var project = new PmsProject("PRJ-ASTER-01", "Aster 实施", customer.Id, "张经理", Today, Today.AddDays(60)); project.SetStatus(PmsProjectStatus.Active);
        var order = SalesOrder.Restore(Guid.CreateVersion7(), "SO-ASTER-01", customer.Id, Guid.CreateVersion7(), Today, 2m, 200m, SalesOrderStatus.Submitted, contract.Id, project.Id);

        var results = CrossModuleSearchService.Build("Aster", CrossModuleSearchScope.All, [customer], [contract], [order], [project]);

        Assert.Equal(["客户", "合同", "销售订单", "项目"], results.Select(item => item.ObjectType).ToArray());
        Assert.Equal("/Erp/SalesOrder?orderId=" + order.Id, results.Single(item => item.ObjectType == "销售订单").Href);
        Assert.DoesNotContain("OtherInfo", string.Join(' ', results.Select(item => item.Summary)));
    }

    [Fact]
    public void Build_HonorsModuleScopeWithoutLeakingExcludedObjects()
    {
        var customer = new Customer("范围客户");
        var contract = new SalesContract(customer.Id, null, "CT-SCOPE-01", "范围合同", 100m, Today, Today.AddDays(1));
        var order = new SalesOrder("SO-SCOPE-01", customer.Id, Guid.CreateVersion7(), Today, 1m, 100m, contract.Id);
        var project = new PmsProject("PRJ-SCOPE-01", "范围项目", customer.Id, null, Today, Today.AddDays(1));

        var results = CrossModuleSearchService.Build("范围客户", new CrossModuleSearchScope(true, false, false, false), [customer], [contract], [order], [project]);

        var result = Assert.Single(results);
        Assert.Equal("客户", result.ObjectType);
        Assert.Equal(customer.Id, result.Id);
    }

    [Fact]
    public void Build_SearchingProjectIncludesLinkedOrderAndPreservesProjectDeepLink()
    {
        var customer = new Customer("项目客户");
        var project = new PmsProject("PRJ-SEARCH-01", "交付搜索项目", customer.Id, "王经理", Today, Today.AddDays(10));
        var linkedOrder = new SalesOrder("SO-PROJECT-01", customer.Id, Guid.CreateVersion7(), Today, 3m, 100m, null, project.Id);
        var otherOrder = new SalesOrder("SO-PROJECT-02", customer.Id, Guid.CreateVersion7(), Today, 1m, 100m);

        var results = CrossModuleSearchService.Build("PRJ-SEARCH-01", new CrossModuleSearchScope(false, false, true, true), [customer], [], [linkedOrder, otherOrder], [project]);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, item => item.Id == linkedOrder.Id);
        Assert.DoesNotContain(results, item => item.Id == otherOrder.Id);
        Assert.Equal("/Pms/Project?projectId=" + project.Id, results.Single(item => item.Id == project.Id).Href);
    }

    [Fact]
    public void Build_SearchesPurchaseSourceAndInventorySourceNumber()
    {
        var supplierId = Guid.CreateVersion7();
        var productId = Guid.CreateVersion7();
        var warehouseId = Guid.CreateVersion7();
        var purchase = PurchaseOrder.Restore(Guid.CreateVersion7(), "PO-SOURCE-01", supplierId, productId, Today, 5m, 20m, PurchaseOrderStatus.Submitted, PurchaseOrderSourceKind.Requisition, "REQ-SEARCH-01");
        var inventory = new InventoryTransaction(productId, warehouseId, InventoryTransactionKind.Inbound, 5m, "REQ-SEARCH-01-IN", Today, "采购收货");

        var results = CrossModuleSearchService.Build("REQ-SEARCH-01", new CrossModuleSearchScope(false, false, false, false, true, true), [], [], [], [], [purchase], [inventory], []);

        Assert.Equal(["采购订单", "库存流水"], results.Select(item => item.ObjectType).ToArray());
        Assert.Equal("/Erp/PurchaseOrder?orderId=" + purchase.Id, results.Single(item => item.Id == purchase.Id).Href);
        Assert.Contains("REQ-SEARCH-01", results.Single(item => item.ObjectType == "库存流水").Href);
    }

    [Fact]
    public void Build_HidesErpResultsWhenTheirMenuScopesAreDisabled()
    {
        var customer = new Customer("核销客户");
        var settlement = new ErpSettlement("REC-SCOPE-01", Guid.CreateVersion7(), customer.Id, ErpSettlementKind.Receivable, 200m, Today);
        var purchase = new PurchaseOrder("PO-SCOPE-01", Guid.CreateVersion7(), Guid.CreateVersion7(), Today, 1m, 100m);

        var results = CrossModuleSearchService.Build("核销客户", new CrossModuleSearchScope(true, false, false, false), [customer], [], [], [], [purchase], [], [settlement]);

        Assert.Single(results);
        Assert.Equal("客户", results[0].ObjectType);
    }

    [Fact]
    public void ResultFacetsAndFilter_OnlyOperateOnAlreadyReturnedResults()
    {
        var customer = new CrossModuleSearchResult("客户", Guid.CreateVersion7(), "Aster", "Aster", "启用", "客户主数据", null, "/Crm/CustomerLedger/a");
        var firstOrder = new CrossModuleSearchResult("销售订单", Guid.CreateVersion7(), "SO-01", "SO-01", "已提交", "金额 ¥100.00", null, "/Erp/SalesOrder?orderId=a");
        var secondOrder = new CrossModuleSearchResult("销售订单", Guid.CreateVersion7(), "SO-02", "SO-02", "已提交", "金额 ¥200.00", null, "/Erp/SalesOrder?orderId=b");

        var facets = CrossModuleSearchService.BuildFacets([customer, firstOrder, secondOrder]);
        var filtered = CrossModuleSearchService.FilterByObjectType([customer, firstOrder, secondOrder], "销售订单");

        Assert.Equal(["客户", "销售订单"], facets.Select(item => item.ObjectType).ToArray());
        Assert.Equal([1, 2], facets.Select(item => item.Count).ToArray());
        Assert.Equal([firstOrder.Id, secondOrder.Id], filtered.Select(item => item.Id).ToArray());
    }

    private static readonly DateOnly Today = new(2026, 7, 28);
}
