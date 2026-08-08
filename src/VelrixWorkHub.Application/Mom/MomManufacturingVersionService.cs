using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public sealed class MomManufacturingVersionService(
    IMomManufacturingVersionRepository versionRepository,
    IMomManufacturingComponentRepository componentRepository,
    IProductRepository productRepository)
{
    public IReadOnlyList<MomManufacturingVersion> List(Guid? productId = null, MomManufacturingVersionStatus? status = null)
    {
        var query = versionRepository.List().AsEnumerable();
        if (productId is Guid selectedProduct) query = query.Where(x => x.ProductId == selectedProduct);
        if (status is MomManufacturingVersionStatus selectedStatus) query = query.Where(x => x.Status == selectedStatus);
        return query.OrderBy(x => x.ProductId).ThenByDescending(x => x.EffectiveFrom).ThenBy(x => x.VersionCode).ToArray();
    }

    public IReadOnlyList<MomManufacturingComponent> ListComponents(Guid manufacturingVersionId)
        => componentRepository.List().Where(x => x.ManufacturingVersionId == manufacturingVersionId).OrderBy(x => x.LineNo).ToArray();

    public MomManufacturingVersion Create(Guid productId, string versionCode, string name, DateOnly effectiveFrom,
        DateOnly? effectiveTo = null, string? engineeringChangeReference = null, string? otherInfo = null)
    {
        EnsureActiveProduct(productId);
        var item = new MomManufacturingVersion(productId, versionCode, name, effectiveFrom, effectiveTo, engineeringChangeReference, otherInfo);
        EnsureUnique(item);
        versionRepository.Add(item);
        return item;
    }

    public void Edit(MomManufacturingVersion item, Guid productId, string versionCode, string name, DateOnly effectiveFrom,
        DateOnly? effectiveTo = null, string? engineeringChangeReference = null, string? otherInfo = null)
    {
        EnsureActiveProduct(productId);
        item.Edit(productId, versionCode, name, effectiveFrom, effectiveTo, engineeringChangeReference, otherInfo);
        EnsureUnique(item);
        versionRepository.Update(item);
    }

    public void AddComponent(Guid manufacturingVersionId, int lineNo, Guid componentProductId, decimal quantityPer,
        decimal scrapRatePercent = 0, int operationSequence = 10, string? notes = null, string? otherInfo = null)
    {
        var version = FindVersion(manufacturingVersionId);
        EnsureDraft(version);
        EnsureActiveProduct(componentProductId);
        if (version.ProductId == componentProductId) throw new InvalidOperationException("制造版本不能把自身产品作为组件。");
        if (componentRepository.List().Any(x => x.ManufacturingVersionId == manufacturingVersionId && x.LineNo == lineNo)) throw new InvalidOperationException("同一制造版本的组件行号已存在。");
        if (componentRepository.List().Any(x => x.ManufacturingVersionId == manufacturingVersionId && x.ComponentProductId == componentProductId)) throw new InvalidOperationException("同一制造版本不能重复添加相同组件商品。");
        componentRepository.Add(new MomManufacturingComponent(manufacturingVersionId, lineNo, componentProductId, quantityPer, scrapRatePercent, operationSequence, notes, otherInfo));
    }

    public void EditComponent(MomManufacturingComponent item, int lineNo, Guid componentProductId, decimal quantityPer,
        decimal scrapRatePercent = 0, int operationSequence = 10, string? notes = null, string? otherInfo = null)
    {
        var version = FindVersion(item.ManufacturingVersionId);
        EnsureDraft(version);
        EnsureActiveProduct(componentProductId);
        if (version.ProductId == componentProductId) throw new InvalidOperationException("制造版本不能把自身产品作为组件。");
        if (componentRepository.List().Any(x => x.Id != item.Id && x.ManufacturingVersionId == item.ManufacturingVersionId && x.LineNo == lineNo)) throw new InvalidOperationException("同一制造版本的组件行号已存在。");
        if (componentRepository.List().Any(x => x.Id != item.Id && x.ManufacturingVersionId == item.ManufacturingVersionId && x.ComponentProductId == componentProductId)) throw new InvalidOperationException("同一制造版本不能重复添加相同组件商品。");
        item.Edit(item.ManufacturingVersionId, lineNo, componentProductId, quantityPer, scrapRatePercent, operationSequence, notes, otherInfo);
        componentRepository.Update(item);
    }

    public void RemoveComponent(MomManufacturingComponent item)
    {
        EnsureDraft(FindVersion(item.ManufacturingVersionId));
        componentRepository.Remove(item.Id);
    }

    public void Release(MomManufacturingVersion item)
    {
        EnsureActiveProduct(item.ProductId);
        EnsureDraft(item);
        if (!ListComponents(item.Id).Any()) throw new InvalidOperationException("制造版本至少需要一个组件才能发布。");
        foreach (var component in ListComponents(item.Id)) EnsureActiveProduct(component.ComponentProductId);
        if (versionRepository.List().Any(x => x.Id != item.Id && x.ProductId == item.ProductId && x.Status == MomManufacturingVersionStatus.Released && Overlaps(x, item))) throw new InvalidOperationException("同一产品存在有效期重叠的已发布制造版本。");
        item.Release();
        versionRepository.Update(item);
    }

    public void Retire(MomManufacturingVersion item)
    {
        item.Retire();
        versionRepository.Update(item);
    }

    private MomManufacturingVersion FindVersion(Guid id) => versionRepository.List().FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("制造版本不存在。");
    private void EnsureDraft(MomManufacturingVersion item) { if (item.Status != MomManufacturingVersionStatus.Draft) throw new InvalidOperationException("只有草稿制造版本可以维护组件。"); }
    private void EnsureActiveProduct(Guid productId) { var product = productRepository.List().FirstOrDefault(x => x.Id == productId) ?? throw new InvalidOperationException("制造版本商品不存在。"); if (product.Status != ProductStatus.Active) throw new InvalidOperationException("停用商品不能用于制造版本。"); }
    private void EnsureUnique(MomManufacturingVersion item) { if (versionRepository.List().Any(x => x.Id != item.Id && x.ProductId == item.ProductId && x.VersionCode.Equals(item.VersionCode, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("同一产品下制造版本编码已存在。"); }
    private static bool Overlaps(MomManufacturingVersion left, MomManufacturingVersion right) => left.EffectiveFrom <= (right.EffectiveTo ?? DateOnly.MaxValue) && right.EffectiveFrom <= (left.EffectiveTo ?? DateOnly.MaxValue);
}
