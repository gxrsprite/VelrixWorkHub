using VelrixWorkHub.Application.MasterData;
using VelrixWorkHub.Application.PurchaseOrders;
using VelrixWorkHub.Application.Settlements;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.Suppliers;
public sealed class SupplierService(
    ISupplierRepository repository,
    IPurchaseOrderRepository? purchaseOrderRepository = null,
    ISettlementRepository? settlementRepository = null)
{
    public IReadOnlyList<Supplier> List(string? keyword = null, SupplierStatus? status = null)
    {
        var query = repository.List().AsEnumerable(); var text = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(text)) query = query.Where(x => x.Code.Contains(text, StringComparison.OrdinalIgnoreCase) || x.Name.Contains(text, StringComparison.OrdinalIgnoreCase) || (x.ContactName?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false));
        if (status is not null) query = query.Where(x => x.Status == status);
        return query.ToArray();
    }
    public Supplier Create(string code, string name, string? contactName, string? phone, string? notes, string? otherInfo = null) { var item = new Supplier(code, name, contactName, phone, notes, otherInfo); EnsureUnique(item); repository.Add(item); return item; }
    public void Edit(Supplier item, string code, string name, string? contactName, string? phone, string? notes, string? otherInfo = null) { item.Edit(code, name, contactName, phone, notes, otherInfo); EnsureUnique(item); repository.Update(item); }
    public void SetActive(Supplier item, bool active) { item.SetActive(active); repository.Update(item); }
    public void SetQualification(Supplier item, SupplierQualificationStatus status) { item.SetQualification(status); repository.Update(item); }
    public void Remove(Supplier item)
    {
        var impact = MasterDataImpactService.Supplier(
            item.Id,
            purchaseOrderRepository?.List() ?? Array.Empty<PurchaseOrder>(),
            settlementRepository?.List() ?? Array.Empty<ErpSettlement>());
        var decision = MasterDataImpactService.Decide(impact);
        if (!decision.CanDelete) throw new InvalidOperationException($"{decision.Reason}{decision.SuggestedAction}");
        repository.Remove(item.Id);
    }
    private void EnsureUnique(Supplier item) { if (repository.List().Any(x => x.Id != item.Id && x.Code.Equals(item.Code, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("供应商编码已存在。"); }
}
