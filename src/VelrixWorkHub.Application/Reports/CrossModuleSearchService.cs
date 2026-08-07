using VelrixWorkHub.Application.Contracts;
using VelrixWorkHub.Application.Customers;
using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Application.PurchaseOrders;
using VelrixWorkHub.Application.SalesOrders;
using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Application.Settlements;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Reports;

public sealed record CrossModuleSearchScope(bool Customers, bool Contracts, bool SalesOrders, bool Projects, bool PurchaseOrders = false, bool Inventory = false, bool Settlements = false)
{
    public static CrossModuleSearchScope All { get; } = new(true, true, true, true, true, true, true);
}

public sealed record CrossModuleSearchResult(
    string ObjectType,
    Guid Id,
    string Code,
    string Title,
    string Status,
    string Summary,
    string? Responsible,
    string Href);

public sealed record CrossModuleSearchFacet(string ObjectType, int Count);

/// <summary>Composes safe, read-only summaries from existing module application services.</summary>
public sealed class CrossModuleSearchService(
    CustomerService customerService,
    SalesContractService contractService,
    SalesOrderService salesOrderService,
    PmsProjectService projectService,
    PurchaseOrderService purchaseOrderService,
    InventoryService inventoryService,
    SettlementService settlementService)
{
    public IReadOnlyList<CrossModuleSearchResult> Search(string? keyword, CrossModuleSearchScope scope, int take = 50)
        => Build(keyword, scope, customerService.List(), contractService.List(), salesOrderService.List(), projectService.List(), purchaseOrderService.List(), inventoryService.List(), settlementService.List(), take);

    public static IReadOnlyList<CrossModuleSearchResult> Build(
        string? keyword,
        CrossModuleSearchScope scope,
        IEnumerable<Customer> customers,
        IEnumerable<SalesContract> contracts,
        IEnumerable<SalesOrder> orders,
        IEnumerable<PmsProject> projects,
        int take = 50)
        => Build(keyword, scope, customers, contracts, orders, projects, [], [], [], take);

    public static IReadOnlyList<CrossModuleSearchResult> Build(
        string? keyword,
        CrossModuleSearchScope scope,
        IEnumerable<Customer> customers,
        IEnumerable<SalesContract> contracts,
        IEnumerable<SalesOrder> orders,
        IEnumerable<PmsProject> projects,
        IEnumerable<PurchaseOrder> purchaseOrders,
        IEnumerable<InventoryTransaction> inventoryTransactions,
        IEnumerable<ErpSettlement> settlements,
        int take = 50)
    {
        var text = keyword?.Trim();
        if (string.IsNullOrWhiteSpace(text) || take <= 0) return [];

        var customerItems = customers.ToArray();
        var contractItems = contracts.ToArray();
        var orderItems = orders.ToArray();
        var projectItems = projects.ToArray();
        var purchaseOrderItems = purchaseOrders.ToArray();
        var inventoryItems = inventoryTransactions.ToArray();
        var settlementItems = settlements.ToArray();
        var matchedCustomerIds = customerItems.Where(item => Contains(item.Name, text) || Contains(item.ContactName, text)).Select(item => item.Id).ToHashSet();
        var matchedContractIds = contractItems.Where(item => Contains(item.ContractNo, text) || Contains(item.Title, text) || matchedCustomerIds.Contains(item.CustomerId)).Select(item => item.Id).ToHashSet();
        var matchedProjectIds = projectItems.Where(item => Contains(item.Code, text) || Contains(item.Name, text) || matchedCustomerIds.Contains(item.CustomerId ?? Guid.Empty)).Select(item => item.Id).ToHashSet();
        var results = new List<CrossModuleSearchResult>();

        if (scope.Customers)
        {
            results.AddRange(customerItems.Where(item => matchedCustomerIds.Contains(item.Id)).Select(item => new CrossModuleSearchResult(
                "客户", item.Id, item.Name, item.Name, CustomerStatusLabel(item.Status), "客户主数据", item.ContactName, $"/Crm/CustomerLedger/{item.Id}")));
        }
        if (scope.Contracts)
        {
            results.AddRange(contractItems.Where(item => matchedContractIds.Contains(item.Id)).Select(item => new CrossModuleSearchResult(
                "合同", item.Id, item.ContractNo, item.Title, ContractStatusLabel(item.Status), $"合同金额 ¥{item.Amount:N2} · {item.StartDate:yyyy-MM-dd} 至 {item.EndDate:yyyy-MM-dd}", null, $"/Crm/ContractLedger/{item.Id}")));
        }
        if (scope.SalesOrders)
        {
            results.AddRange(orderItems.Where(item => Contains(item.OrderNo, text) || matchedCustomerIds.Contains(item.CustomerId) || matchedContractIds.Contains(item.ContractId ?? Guid.Empty) || matchedProjectIds.Contains(item.PmsProjectId ?? Guid.Empty)).Select(item => new CrossModuleSearchResult(
                "销售订单", item.Id, item.OrderNo, item.OrderNo, SalesOrderStatusLabel(item.Status), $"金额 ¥{item.Amount:N2} · 数量 {item.Quantity:N2} · 收款到期 {item.DueDate:yyyy-MM-dd}", null, $"/Erp/SalesOrder?orderId={item.Id}")));
        }
        if (scope.Projects)
        {
            results.AddRange(projectItems.Where(item => matchedProjectIds.Contains(item.Id)).Select(item => new CrossModuleSearchResult(
                "项目", item.Id, item.Code, item.Name, ProjectStatusLabel(item.Status), $"计划 {item.PlannedStart:yyyy-MM-dd} 至 {item.PlannedEnd:yyyy-MM-dd} · 完成度 {item.PercentComplete}%", item.ManagerName, $"/Pms/Project?projectId={item.Id}")));
        }
        if (scope.PurchaseOrders)
        {
            results.AddRange(purchaseOrderItems.Where(item => Contains(item.OrderNo, text) || Contains(item.SourceDocumentNo, text)).Select(item => new CrossModuleSearchResult(
                "采购订单", item.Id, item.OrderNo, item.OrderNo, PurchaseOrderStatusLabel(item.Status), $"金额 ¥{item.Amount:N2} · 数量 {item.Quantity:N2} · 付款到期 {item.DueDate:yyyy-MM-dd}" + (string.IsNullOrWhiteSpace(item.SourceDocumentNo) ? string.Empty : $" · 来源 {item.SourceDocumentNo}"), null, $"/Erp/PurchaseOrder?orderId={item.Id}")));
        }
        if (scope.Inventory)
        {
            results.AddRange(inventoryItems.Where(item => Contains(item.SourceNo, text)).Select(item => new CrossModuleSearchResult(
                "库存流水", item.Id, item.SourceNo, item.SourceNo, InventoryKindLabel(item.Kind), $"数量 {item.SignedQuantity:N2} · 发生日 {item.OccurredOn:yyyy-MM-dd}", null, $"/Erp/Inventory?keyword={Uri.EscapeDataString(item.SourceNo)}")));
        }
        if (scope.Settlements)
        {
            results.AddRange(settlementItems.Where(item => Contains(item.ReferenceNo, text) || matchedCustomerIds.Contains(item.PartyId)).Select(item => new CrossModuleSearchResult(
                "收付款核销", item.Id, item.ReferenceNo, item.ReferenceNo, SettlementStatusLabel(item), $"{(item.Kind == ErpSettlementKind.Receivable ? "收款" : "付款")} ¥{item.Amount:N2} · {item.OccurredOn:yyyy-MM-dd}", null, $"/Erp/Settlement?settlementId={item.Id}")));
        }

        return results.OrderBy(item => TypeOrder(item.ObjectType)).ThenBy(item => item.Code, StringComparer.OrdinalIgnoreCase).Take(take).ToArray();
    }

    public static IReadOnlyList<CrossModuleSearchFacet> BuildFacets(IEnumerable<CrossModuleSearchResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return results
            .GroupBy(item => item.ObjectType, StringComparer.Ordinal)
            .Select(group => new CrossModuleSearchFacet(group.Key, group.Count()))
            .OrderBy(item => TypeOrder(item.ObjectType))
            .ThenBy(item => item.ObjectType, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<CrossModuleSearchResult> FilterByObjectType(IEnumerable<CrossModuleSearchResult> results, string? objectType)
    {
        ArgumentNullException.ThrowIfNull(results);
        return string.IsNullOrWhiteSpace(objectType)
            ? results.ToArray()
            : results.Where(item => item.ObjectType.Equals(objectType.Trim(), StringComparison.Ordinal)).ToArray();
    }

    private static bool Contains(string? value, string text) => value?.Contains(text, StringComparison.OrdinalIgnoreCase) == true;
    private static int TypeOrder(string type) => type switch { "客户" => 0, "合同" => 1, "销售订单" => 2, "采购订单" => 3, "收付款核销" => 4, "库存流水" => 5, "项目" => 6, _ => 9 };
    private static string CustomerStatusLabel(CustomerStatus status) => status == CustomerStatus.Active ? "启用" : "停用";
    private static string ContractStatusLabel(ContractStatus status) => status switch { ContractStatus.Draft => "草稿", ContractStatus.Active => "生效", ContractStatus.Terminated => "已终止", _ => status.ToString() };
    private static string SalesOrderStatusLabel(SalesOrderStatus status) => status switch { SalesOrderStatus.Draft => "草稿", SalesOrderStatus.Submitted => "已提交", SalesOrderStatus.Shipped => "已发货", SalesOrderStatus.Cancelled => "已取消", _ => status.ToString() };
    private static string PurchaseOrderStatusLabel(PurchaseOrderStatus status) => status switch { PurchaseOrderStatus.Draft => "草稿", PurchaseOrderStatus.Submitted => "已提交", PurchaseOrderStatus.Received => "已收货", PurchaseOrderStatus.Cancelled => "已取消", PurchaseOrderStatus.Closed => "已关闭", _ => status.ToString() };
    private static string InventoryKindLabel(InventoryTransactionKind kind) => kind switch { InventoryTransactionKind.Inbound => "入库", InventoryTransactionKind.Outbound => "出库", _ => "库存调整" };
    private static string SettlementStatusLabel(ErpSettlement settlement) => settlement.Status switch { ErpSettlementStatus.PendingApproval => "待审批", ErpSettlementStatus.Rejected => "审批拒绝", ErpSettlementStatus.Active => settlement.Kind == ErpSettlementKind.Receivable ? "有效收款" : "有效付款", _ => "已撤销" };
    private static string ProjectStatusLabel(PmsProjectStatus status) => status switch { PmsProjectStatus.Draft => "草稿", PmsProjectStatus.Active => "进行中", PmsProjectStatus.OnHold => "暂停", PmsProjectStatus.Completed => "已完成", PmsProjectStatus.Cancelled => "已取消", _ => status.ToString() };
}
