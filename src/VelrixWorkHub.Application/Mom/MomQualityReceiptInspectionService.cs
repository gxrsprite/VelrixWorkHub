using VelrixWorkHub.Application.PurchaseOrders;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

/// <summary>
/// MOM-07E 采购收货 IQC/SQC 质量门禁。没有配置质量关联的历史采购订单保持原有收货兼容。
/// </summary>
public sealed class MomQualityReceiptInspectionService(
    IMomQualityReceiptInspectionRepository repository,
    IPurchaseOrderRepository purchaseOrderRepository,
    IMomQualityInspectionRepository inspectionRepository,
    VelrixWorkHub.Application.Workflow.IWorkflowTransactionBoundary? transactions = null) : IMomQualityReceiptGate
{
    public IReadOnlyList<MomQualityReceiptInspection> List(Guid? purchaseOrderId = null)
    {
        var query = repository.List().AsEnumerable();
        if (purchaseOrderId is Guid selected) query = query.Where(x => x.PurchaseOrderId == selected);
        return query.OrderByDescending(x => x.LinkedOn).ThenByDescending(x => x.InspectionNo).ToArray();
    }

    public MomQualityReceiptInspection Link(Guid purchaseOrderId, Guid inspectionId, DateTime? linkedOn = null, string? otherInfo = null)
    {
        var purchaseOrder = purchaseOrderRepository.List().FirstOrDefault(x => x.Id == purchaseOrderId)
            ?? throw new InvalidOperationException("采购订单不存在。");
        if (purchaseOrder.Status != PurchaseOrderStatus.Submitted)
            throw new InvalidOperationException("只有已提交的采购订单可以关联收货质量检验。");
        var inspection = inspectionRepository.List().FirstOrDefault(x => x.Id == inspectionId)
            ?? throw new InvalidOperationException("质量检验记录不存在。");
        if (inspection.InspectionType is not (MomQualityInspectionType.Iqc or MomQualityInspectionType.Sqc))
            throw new InvalidOperationException("采购收货质量关联只能选择 IQC 或 SQC 检验。");
        if (inspection.ProductId != purchaseOrder.ProductId)
            throw new InvalidOperationException("收货质量检验商品必须与采购订单商品一致。");
        if (repository.List().Any(x => x.PurchaseOrderId == purchaseOrderId && x.InspectionId == inspectionId))
            throw new InvalidOperationException("该质量检验已经关联采购订单。");
        var item = new MomQualityReceiptInspection(purchaseOrderId, inspection, linkedOn ?? DateTime.Now, otherInfo);
        void Persist() => repository.Add(item);
        if (transactions is null) Persist(); else transactions.Execute(Persist);
        return item;
    }

    public void EnsureCanReceive(Guid purchaseOrderId, Guid productId)
    {
        var purchaseOrder = purchaseOrderRepository.List().FirstOrDefault(x => x.Id == purchaseOrderId)
            ?? throw new InvalidOperationException("采购订单不存在。");
        if (purchaseOrder.ProductId != productId) throw new InvalidOperationException("采购订单商品引用不一致。");
        var links = repository.List().Where(x => x.PurchaseOrderId == purchaseOrderId).ToArray();
        if (links.Length == 0) return;
        if (links.Any(x => x.ProductId != productId)) throw new InvalidOperationException("采购收货质量关联商品引用不一致。");
        var inspectionRecords = inspectionRepository.List();
        if (links.Any(link => inspectionRecords.All(x => x.Id != link.InspectionId))) throw new InvalidOperationException("收货质量检验记录不存在。");
        var inspections = inspectionRecords.Where(x => links.Any(link => link.InspectionId == x.Id && x.Status != MomQualityInspectionStatus.Cancelled)).ToArray();
        if (inspections.Length == 0) throw new InvalidOperationException("采购订单已配置收货质量检验，但所有关联检验均已取消，请重新关联。");
        if (inspections.Any(x => x.Status == MomQualityInspectionStatus.Pending)) throw new InvalidOperationException("采购订单存在待检质量记录，不能收货。");
        if (inspections.Any(x => x.Status == MomQualityInspectionStatus.Failed)) throw new InvalidOperationException("采购订单存在不通过质量检验，不能收货。");
        if (inspections.Any(x => x.Status != MomQualityInspectionStatus.Passed)) throw new InvalidOperationException("采购订单收货质量检验未全部通过。");
    }
}
