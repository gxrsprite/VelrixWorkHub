using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

/// <summary>
/// MOM-07A 工序质量检验首版。统一保存 IQC/IPQC/FQC/SQC 记录，IPQC 最新结果作为工序完工门禁。
/// </summary>
public sealed class MomQualityInspectionService(
    IMomQualityInspectionRepository repository,
    IMomWorkOrderRepository workOrderRepository,
    IMomWorkOrderOperationRepository operationRepository,
    IProductRepository productRepository,
    IWorkflowTransactionBoundary? transactions = null,
    MomQualityInspectionStandardService? standardService = null) : IMomQualityInspectionGate
{
    public IReadOnlyList<MomQualityInspection> List(Guid? workOrderId = null, Guid? operationId = null,
        MomQualityInspectionType? inspectionType = null)
    {
        var query = repository.List().AsEnumerable();
        if (workOrderId is Guid workOrder) query = query.Where(x => x.WorkOrderId == workOrder);
        if (operationId is Guid operation) query = query.Where(x => x.OperationId == operation);
        if (inspectionType is MomQualityInspectionType type) query = query.Where(x => x.InspectionType == type);
        return query.OrderByDescending(x => x.CreatedOn).ThenByDescending(x => x.InspectionNo).ToArray();
    }

    public MomQualityInspection Create(Guid workOrderId, MomQualityInspectionType inspectionType, Guid? operationId,
        Guid? productId, string? batchNo, string? serialNo, decimal sampleQuantity, string? notes = null, string? otherInfo = null,
        DateTime? createdOn = null, Guid? standardId = null)
    {
        var workOrder = FindWorkOrder(workOrderId);
        if (workOrder.Status is MomWorkOrderStatus.Cancelled or MomWorkOrderStatus.Completed)
            throw new InvalidOperationException("已取消或已完工工单不能新增质量检验。");

        MomWorkOrderOperation? operation = null;
        if (operationId is Guid selectedOperationId)
        {
            operation = operationRepository.List().FirstOrDefault(x => x.Id == selectedOperationId)
                ?? throw new InvalidOperationException("质量检验工序不存在。");
            if (operation.WorkOrderId != workOrder.Id) throw new InvalidOperationException("质量检验工序不属于当前工单。");
            if (operation.Status == MomOperationStatus.Cancelled) throw new InvalidOperationException("已取消工序不能新增质量检验。");
        }
        if (inspectionType == MomQualityInspectionType.Ipqc && operation is null)
            throw new InvalidOperationException("IPQC 质量检验必须绑定工序。");
        if (operation is not null && inspectionType != MomQualityInspectionType.Ipqc)
            throw new InvalidOperationException("只有 IPQC 质量检验可以绑定工序。");

        if (productId is Guid selectedProductId)
        {
            var product = productRepository.List().FirstOrDefault(x => x.Id == selectedProductId)
                ?? throw new InvalidOperationException("质量检验商品不存在。");
            if (product.Status != ProductStatus.Active) throw new InvalidOperationException("停用商品不能用于质量检验。");
            if (product.Id != workOrder.ProductId) throw new InvalidOperationException("质量检验商品必须与工单商品一致。");
        }
        else if (inspectionType is MomQualityInspectionType.Iqc or MomQualityInspectionType.Fqc or MomQualityInspectionType.Sqc)
        {
            throw new InvalidOperationException("该质量检验类型必须选择商品。");
        }

        var normalizedBatch = Clean(batchNo);
        var duplicatePending = repository.List().Any(x => x.WorkOrderId == workOrder.Id
            && x.InspectionType == inspectionType && x.OperationId == operationId
            && string.Equals(x.BatchNo, normalizedBatch, StringComparison.OrdinalIgnoreCase)
            && x.Status == MomQualityInspectionStatus.Pending);
        if (duplicatePending) throw new InvalidOperationException("同一工单、检验类型和批次不能重复存在待检记录。");

        var standardProductId = productId ?? workOrder.ProductId;
        Guid? resolvedStandardId = null;
        string? standardCode = null;
        string? standardVersion = null;
        string? standardSnapshotJson = null;
        if (standardId is Guid selectedStandardId)
        {
            if (standardService is null) throw new InvalidOperationException("质量标准服务未配置。");
            var snapshot = standardService.GetActiveSnapshot(selectedStandardId, inspectionType, standardProductId)
                ?? throw new InvalidOperationException("质量标准不存在、未启用或与检验类型/商品不匹配。");
            resolvedStandardId = snapshot.Standard.Id;
            standardCode = snapshot.Standard.Code;
            standardVersion = snapshot.Standard.Version;
            standardSnapshotJson = snapshot.SnapshotJson;
        }
        var item = new MomQualityInspection(workOrder.Id, inspectionType, operationId, productId, normalizedBatch, serialNo,
            sampleQuantity, createdOn ?? DateTime.Now, notes, otherInfo, standardId: resolvedStandardId, standardCode: standardCode,
            standardVersion: standardVersion, standardSnapshotJson: standardSnapshotJson);
        void Persist() => repository.Add(item);
        if (transactions is null) Persist(); else transactions.Execute(Persist);
        return item;
    }

    public MomQualityInspection RecordResult(Guid inspectionId, decimal acceptedQuantity, decimal rejectedQuantity,
        string inspector, DateTime? inspectedOn = null, string? notes = null)
    {
        var item = Find(inspectionId);
        var workOrder = FindWorkOrder(item.WorkOrderId);
        if (workOrder.Status is MomWorkOrderStatus.Cancelled or MomWorkOrderStatus.Completed)
            throw new InvalidOperationException("已取消或已完工工单不能登记质量结果。");
        var original = Snapshot(item);
        item.RecordResult(acceptedQuantity, rejectedQuantity, inspector, inspectedOn ?? DateTime.Now);
        if (!string.IsNullOrWhiteSpace(notes)) item.UpdateNotes(notes);
        void Persist() => repository.Update(item);
        if (transactions is null) Persist();
        else transactions.Execute(Persist, _ => item.RestoreResult(original.Status, original.AcceptedQuantity, original.RejectedQuantity, original.Inspector, original.InspectedOn));
        return item;
    }

    public void Cancel(Guid inspectionId)
    {
        var item = Find(inspectionId);
        var original = item.Status;
        item.Cancel();
        void Persist() => repository.Update(item);
        if (transactions is null) Persist();
        else transactions.Execute(Persist, _ => item.RestoreResult(original, 0, 0, null, null));
    }

    public void EnsureOperationCanComplete(Guid operationId)
    {
        var latest = repository.List().Where(x => x.OperationId == operationId && x.InspectionType == MomQualityInspectionType.Ipqc
                && x.Status != MomQualityInspectionStatus.Cancelled)
            .OrderByDescending(x => x.CreatedOn).ThenByDescending(x => x.InspectionNo).FirstOrDefault();
        if (latest is not null && latest.Status != MomQualityInspectionStatus.Passed)
            throw new InvalidOperationException("工序存在未通过的质量检验，不能完工。");
    }

    public void EnsureWorkOrderCanComplete(Guid workOrderId)
    {
        var latest = repository.List().Where(x => x.WorkOrderId == workOrderId && x.InspectionType == MomQualityInspectionType.Fqc
                && x.Status != MomQualityInspectionStatus.Cancelled)
            .OrderByDescending(x => x.CreatedOn).ThenByDescending(x => x.InspectionNo).FirstOrDefault();
        if (latest is not null && latest.Status != MomQualityInspectionStatus.Passed)
            throw new InvalidOperationException("工单存在未通过的 FQC 质量检验，不能完工。");
    }

    private MomQualityInspection Find(Guid id) => repository.List().FirstOrDefault(x => x.Id == id)
        ?? throw new InvalidOperationException("质量检验记录不存在。");
    private MomWorkOrder FindWorkOrder(Guid id) => workOrderRepository.List().FirstOrDefault(x => x.Id == id)
        ?? throw new InvalidOperationException("制造工单不存在。");
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static InspectionSnapshot Snapshot(MomQualityInspection item) => new(item.Status, item.AcceptedQuantity, item.RejectedQuantity, item.Inspector, item.InspectedOn);
    private sealed record InspectionSnapshot(MomQualityInspectionStatus Status, decimal AcceptedQuantity, decimal RejectedQuantity, string? Inspector, DateTime? InspectedOn);
}
