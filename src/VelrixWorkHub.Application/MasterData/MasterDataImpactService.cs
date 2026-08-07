using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.MasterData;

public sealed record MasterDataImpactDecision(
    bool CanDelete,
    string Reason,
    string SuggestedAction,
    IReadOnlyList<string> ImpactObjects);

public sealed record ProductMasterDataImpact(
    Guid ProductId,
    int PurchaseOrderReferenceCount,
    int SalesOrderReferenceCount,
    int InventoryTransactionReferenceCount,
    decimal OnHandQuantity)
{
    public bool HasReferences => PurchaseOrderReferenceCount > 0 || SalesOrderReferenceCount > 0 || InventoryTransactionReferenceCount > 0;
}

public sealed record WarehouseMasterDataImpact(
    Guid WarehouseId,
    int InventoryTransactionReferenceCount,
    decimal OnHandQuantity)
{
    public bool HasReferences => InventoryTransactionReferenceCount > 0;
}

public sealed record CustomerMasterDataImpact(
    Guid CustomerId,
    int ContactReferenceCount,
    int FollowUpReferenceCount,
    int ContractReferenceCount,
    int SalesOrderReferenceCount,
    int ProjectReferenceCount,
    int SettlementReferenceCount)
{
    public bool HasReferences => ContactReferenceCount > 0 || FollowUpReferenceCount > 0 || ContractReferenceCount > 0 || SalesOrderReferenceCount > 0 || ProjectReferenceCount > 0 || SettlementReferenceCount > 0;
}

public sealed record SupplierMasterDataImpact(
    Guid SupplierId,
    int PurchaseOrderReferenceCount,
    int SettlementReferenceCount)
{
    public bool HasReferences => PurchaseOrderReferenceCount > 0 || SettlementReferenceCount > 0;
}

public static class MasterDataImpactService
{
    public static MasterDataImpactDecision Decide(string entityName, params (string Type, int Count)[] references)
    {
        var impacts = references.Where(x => x.Count > 0).Select(x => $"{x.Type} {x.Count} 条").ToArray();
        return impacts.Length == 0
            ? new MasterDataImpactDecision(true, string.Empty, $"可删除{entityName}。", impacts)
            : new MasterDataImpactDecision(false, $"{entityName}存在业务引用，不能删除。", $"请停用{entityName}并保留历史数据。", impacts);
    }

    public static ProductMasterDataImpact Product(Guid productId, IEnumerable<PurchaseOrder> purchaseOrders, IEnumerable<SalesOrder> salesOrders, IEnumerable<InventoryTransaction> transactions)
    {
        var purchase = purchaseOrders.Where(x => x.ProductId == productId).ToArray();
        var sales = salesOrders.Where(x => x.ProductId == productId).ToArray();
        var inventory = transactions.Where(x => x.ProductId == productId).ToArray();
        return new ProductMasterDataImpact(productId, purchase.Length, sales.Length, inventory.Length, inventory.Sum(x => x.SignedQuantity));
    }

    public static WarehouseMasterDataImpact Warehouse(Guid warehouseId, IEnumerable<InventoryTransaction> transactions)
    {
        var inventory = transactions.Where(x => x.WarehouseId == warehouseId).ToArray();
        return new WarehouseMasterDataImpact(warehouseId, inventory.Length, inventory.Sum(x => x.SignedQuantity));
    }

    public static CustomerMasterDataImpact Customer(
        Guid customerId,
        IEnumerable<CustomerContact> contacts,
        IEnumerable<CustomerFollowUp> followUps,
        IEnumerable<SalesContract> contracts,
        IEnumerable<SalesOrder> salesOrders,
        IEnumerable<PmsProject> projects,
        IEnumerable<ErpSettlement> settlements)
    {
        return new CustomerMasterDataImpact(
            customerId,
            contacts.Count(x => x.CustomerId == customerId),
            followUps.Count(x => x.CustomerId == customerId),
            contracts.Count(x => x.CustomerId == customerId),
            salesOrders.Count(x => x.CustomerId == customerId),
            projects.Count(x => x.CustomerId == customerId),
            settlements.Count(x => x.PartyId == customerId));
    }

    public static SupplierMasterDataImpact Supplier(Guid supplierId, IEnumerable<PurchaseOrder> purchaseOrders, IEnumerable<ErpSettlement> settlements)
    {
        return new SupplierMasterDataImpact(
            supplierId,
            purchaseOrders.Count(x => x.SupplierId == supplierId),
            settlements.Count(x => x.PartyId == supplierId));
    }

    public static MasterDataImpactDecision Decide(ProductMasterDataImpact impact) => Decide(
        "商品",
        ("采购订单", impact.PurchaseOrderReferenceCount),
        ("销售订单", impact.SalesOrderReferenceCount),
        ("库存流水", impact.InventoryTransactionReferenceCount));

    public static MasterDataImpactDecision Decide(WarehouseMasterDataImpact impact) => Decide(
        "仓库",
        ("库存流水", impact.InventoryTransactionReferenceCount));

    public static MasterDataImpactDecision Decide(CustomerMasterDataImpact impact) => Decide(
        "客户",
        ("联系人", impact.ContactReferenceCount),
        ("跟进", impact.FollowUpReferenceCount),
        ("合同", impact.ContractReferenceCount),
        ("销售订单", impact.SalesOrderReferenceCount),
        ("项目", impact.ProjectReferenceCount),
        ("核销", impact.SettlementReferenceCount));

    public static MasterDataImpactDecision Decide(SupplierMasterDataImpact impact) => Decide(
        "供应商",
        ("采购订单", impact.PurchaseOrderReferenceCount),
        ("核销", impact.SettlementReferenceCount));
}
