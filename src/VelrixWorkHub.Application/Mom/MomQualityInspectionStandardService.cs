using System.Text.Json;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

/// <summary>
/// MOM-07B 质量标准与检验项目。草稿可维护，发布后只读；质量记录创建时取已发布版本快照。
/// </summary>
public sealed class MomQualityInspectionStandardService(
    IMomQualityInspectionStandardRepository standardRepository,
    IMomQualityInspectionStandardItemRepository itemRepository,
    IProductRepository productRepository,
    IWorkflowTransactionBoundary? transactions = null)
{
    public IReadOnlyList<MomQualityInspectionStandard> List(Guid? productId = null, MomQualityInspectionType? inspectionType = null,
        MomQualityInspectionStandardStatus? status = null)
    {
        var query = standardRepository.List().AsEnumerable();
        if (productId is Guid product) query = query.Where(x => x.ProductId is null || x.ProductId == product);
        if (inspectionType is MomQualityInspectionType type) query = query.Where(x => x.InspectionType == type);
        if (status is MomQualityInspectionStandardStatus selectedStatus) query = query.Where(x => x.Status == selectedStatus);
        return query.OrderBy(x => x.InspectionType).ThenBy(x => x.Code).ThenByDescending(x => x.Version).ToArray();
    }

    public IReadOnlyList<MomQualityInspectionStandardItem> ListItems(Guid standardId)
        => itemRepository.List().Where(x => x.StandardId == standardId).OrderBy(x => x.LineNo).ToArray();

    public MomQualityInspectionStandard Create(Guid? productId, MomQualityInspectionType inspectionType, string code,
        string name, string version, string? otherInfo = null)
    {
        EnsureProduct(productId);
        var item = new MomQualityInspectionStandard(productId, inspectionType, code, name, version, otherInfo);
        EnsureUnique(item);
        void Persist() => standardRepository.Add(item);
        if (transactions is null) Persist(); else transactions.Execute(Persist);
        return item;
    }

    public void Edit(MomQualityInspectionStandard item, Guid? productId, MomQualityInspectionType inspectionType,
        string code, string name, string version, string? otherInfo = null)
    {
        EnsureDraft(item); EnsureProduct(productId); EnsureUniqueCandidate(item, productId, inspectionType, code, version);
        var originalProductId = item.ProductId; var originalType = item.InspectionType; var originalCode = item.Code; var originalName = item.Name; var originalVersion = item.Version; var originalOtherInfo = item.OtherInfo;
        item.Edit(productId, inspectionType, code, name, version, otherInfo);
        void Persist() => standardRepository.Update(item);
        if (transactions is null) Persist(); else transactions.Execute(Persist, _ => item.Edit(originalProductId, originalType, originalCode, originalName, originalVersion, originalOtherInfo));
    }

    public MomQualityInspectionStandardItem AddItem(Guid standardId, int lineNo, string code, string name, string requirement,
        string? unit, decimal? minValue, decimal? maxValue, string? otherInfo = null)
    {
        var standard = Find(standardId); EnsureDraft(standard);
        if (itemRepository.List().Any(x => x.StandardId == standardId && (x.LineNo == lineNo || x.Code.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase))))
            throw new InvalidOperationException("同一质量标准的检验项目行号和编码不能重复。");
        var item = new MomQualityInspectionStandardItem(standardId, lineNo, code, name, requirement, unit, minValue, maxValue, otherInfo);
        void Persist() => itemRepository.Add(item);
        if (transactions is null) Persist(); else transactions.Execute(Persist);
        return item;
    }

    public void EditItem(MomQualityInspectionStandardItem item, int lineNo, string code, string name, string requirement,
        string? unit, decimal? minValue, decimal? maxValue, string? otherInfo = null)
    {
        var standard = Find(item.StandardId); EnsureDraft(standard);
        if (itemRepository.List().Any(x => x.Id != item.Id && x.StandardId == item.StandardId
            && (x.LineNo == lineNo || x.Code.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase))))
            throw new InvalidOperationException("同一质量标准的检验项目行号和编码不能重复。");
        var originalLineNo = item.LineNo; var originalCode = item.Code; var originalName = item.Name; var originalRequirement = item.Requirement; var originalUnit = item.Unit; var originalMinValue = item.MinValue; var originalMaxValue = item.MaxValue; var originalOtherInfo = item.OtherInfo;
        item.Edit(lineNo, code, name, requirement, unit, minValue, maxValue, otherInfo);
        void Persist() => itemRepository.Update(item);
        if (transactions is null) Persist(); else transactions.Execute(Persist, _ => item.RestoreForRecovery(originalLineNo, originalCode, originalName, originalRequirement, originalUnit, originalMinValue, originalMaxValue, originalOtherInfo));
    }

    public void RemoveItem(MomQualityInspectionStandardItem item)
    {
        EnsureDraft(Find(item.StandardId));
        void Persist() => itemRepository.Remove(item.Id);
        if (transactions is null) Persist(); else transactions.Execute(Persist);
    }

    public void Publish(MomQualityInspectionStandard item)
    {
        EnsureDraft(item);
        if (ListItems(item.Id).Count == 0) throw new InvalidOperationException("质量标准至少需要一个检验项目才能发布。");
        item.Publish();
        void Persist() => standardRepository.Update(item);
        if (transactions is null) Persist(); else transactions.Execute(Persist, _ => item.RestoreStatus(MomQualityInspectionStandardStatus.Draft));
    }

    public void Retire(MomQualityInspectionStandard item)
    {
        item.Retire();
        void Persist() => standardRepository.Update(item);
        if (transactions is null) Persist(); else transactions.Execute(Persist, _ => item.RestoreStatus(MomQualityInspectionStandardStatus.Active));
    }

    public (MomQualityInspectionStandard Standard, string SnapshotJson)? GetActiveSnapshot(Guid standardId,
        MomQualityInspectionType inspectionType, Guid productId)
    {
        var standard = standardRepository.List().FirstOrDefault(x => x.Id == standardId && x.Status == MomQualityInspectionStandardStatus.Active);
        if (standard is null || standard.InspectionType != inspectionType || (standard.ProductId is Guid selected && selected != productId)) return null;
        var items = ListItems(standard.Id);
        if (items.Count == 0) throw new InvalidOperationException("已发布质量标准没有检验项目。");
        var snapshot = new MomQualityStandardSnapshot(standard.Id, standard.ProductId, standard.InspectionType, standard.Code, standard.Name, standard.Version,
            items.Select(x => new MomQualityStandardItemSnapshot(x.LineNo, x.Code, x.Name, x.Requirement, x.Unit, x.MinValue, x.MaxValue)).ToArray());
        return (standard, JsonSerializer.Serialize(snapshot, JsonSerializationDefaults.CreateWeb()));
    }

    private MomQualityInspectionStandard Find(Guid id) => standardRepository.List().FirstOrDefault(x => x.Id == id)
        ?? throw new InvalidOperationException("质量标准不存在。");
    private void EnsureUnique(MomQualityInspectionStandard item)
        => EnsureUniqueCandidate(item, item.ProductId, item.InspectionType, item.Code, item.Version);

    private void EnsureUniqueCandidate(MomQualityInspectionStandard item, Guid? productId, MomQualityInspectionType inspectionType, string code, string version)
    {
        var selectedCode = code.Trim(); var selectedVersion = version.Trim();
        if (standardRepository.List().Any(x => x.Id != item.Id && x.InspectionType == inspectionType && x.ProductId == productId
            && x.Code.Equals(selectedCode, StringComparison.OrdinalIgnoreCase) && x.Version.Equals(selectedVersion, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("相同检验类型、商品、编码和版本的质量标准已存在。");
    }
    private void EnsureProduct(Guid? productId)
    {
        if (productId is not Guid selected) return;
        var product = productRepository.List().FirstOrDefault(x => x.Id == selected) ?? throw new InvalidOperationException("质量标准商品不存在。");
        if (product.Status != ProductStatus.Active) throw new InvalidOperationException("停用商品不能绑定质量标准。");
    }
    private static void EnsureDraft(MomQualityInspectionStandard item)
    {
        if (item.Status != MomQualityInspectionStandardStatus.Draft) throw new InvalidOperationException("只有草稿质量标准可以维护。");
    }
}
