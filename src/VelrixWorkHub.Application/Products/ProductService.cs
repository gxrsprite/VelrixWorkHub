using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Application.MasterData;
using VelrixWorkHub.Application.PurchaseOrders;
using VelrixWorkHub.Application.SalesOrders;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.Products;
public sealed class ProductService(
    IProductRepository repository,
    IPurchaseOrderRepository? purchaseOrderRepository = null,
    ISalesOrderRepository? salesOrderRepository = null,
    IInventoryTransactionRepository? inventoryRepository = null)
{
    public IReadOnlyList<Product> List(string? keyword = null, ProductStatus? status = null)
    {
        var query = repository.List().AsEnumerable(); var text = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(text)) query = query.Where(x => x.Code.Contains(text, StringComparison.OrdinalIgnoreCase) || x.Name.Contains(text, StringComparison.OrdinalIgnoreCase));
        if (status is not null) query = query.Where(x => x.Status == status);
        return query.ToArray();
    }
    public Product Create(string code, string name, string unit, decimal? salePrice, string? notes, decimal? maxPurchaseQuantity = null, decimal? safetyStock = null, string? otherInfo = null, decimal? maxInventoryQuantity = null) { var item = new Product(code, name, unit, salePrice, notes, maxPurchaseQuantity, safetyStock, otherInfo, maxInventoryQuantity); EnsureUnique(item); repository.Add(item); return item; }
    public void Edit(Product item, string code, string name, string unit, decimal? salePrice, string? notes, decimal? maxPurchaseQuantity = null, decimal? safetyStock = null, string? otherInfo = null, decimal? maxInventoryQuantity = null) { item.Edit(code, name, unit, salePrice, notes, maxPurchaseQuantity, safetyStock, otherInfo, maxInventoryQuantity); EnsureUnique(item); repository.Update(item); }
    public void SetActive(Product item, bool active) { item.SetActive(active); repository.Update(item); }
    public void Remove(Product item)
    {
        var impact = MasterDataImpactService.Product(
            item.Id,
            purchaseOrderRepository?.List() ?? Array.Empty<PurchaseOrder>(),
            salesOrderRepository?.List() ?? Array.Empty<SalesOrder>(),
            inventoryRepository?.List() ?? Array.Empty<InventoryTransaction>());
        var decision = MasterDataImpactService.Decide(impact);
        if (!decision.CanDelete) throw new InvalidOperationException($"{decision.Reason}{decision.SuggestedAction}");
        repository.Remove(item.Id);
    }
    private void EnsureUnique(Product item) { if (repository.List().Any(x => x.Id != item.Id && x.Code.Equals(item.Code, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("商品编码已存在。"); }
}
