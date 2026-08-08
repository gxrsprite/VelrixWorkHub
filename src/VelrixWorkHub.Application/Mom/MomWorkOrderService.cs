using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.SalesOrders;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public sealed class MomWorkOrderService(
    IMomWorkOrderRepository repository,
    IProductRepository productRepository,
    ISalesOrderRepository? salesOrderRepository = null,
    IPmsProjectRepository? pmsProjectRepository = null,
    IMomWorkCenterRepository? workCenterRepository = null,
    IMomFactoryRepository? factoryRepository = null,
    IMomQualityInspectionGate? qualityInspectionGate = null,
    IMomOperationCompletionGate? operationCompletionGate = null)
{
    public IReadOnlyList<MomWorkOrder> List(string? keyword = null, MomWorkOrderStatus? status = null)
    {
        var query = repository.List().AsEnumerable(); var text = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(text)) query = query.Where(x => x.WorkOrderNo.Contains(text, StringComparison.OrdinalIgnoreCase) || (x.SourceDocumentNo?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false));
        if (status is MomWorkOrderStatus selected) query = query.Where(x => x.Status == selected);
        return query.OrderByDescending(x => x.PlannedStart).ThenByDescending(x => x.WorkOrderNo).ToArray();
    }

    public MomWorkOrder Create(string workOrderNo, Guid productId, DateOnly plannedStart, DateOnly plannedEnd, decimal plannedQuantity,
        MomWorkOrderSourceKind sourceKind = MomWorkOrderSourceKind.Manual, string? sourceDocumentNo = null,
        Guid? salesOrderId = null, Guid? pmsProjectId = null, Guid? workCenterId = null, string? otherInfo = null)
    {
        var product = productRepository.List().FirstOrDefault(x => x.Id == productId) ?? throw new InvalidOperationException("制造商品不存在。");
        if (product.Status != ProductStatus.Active) throw new InvalidOperationException("制造商品已停用，不能创建制造工单。");
        var salesOrder = ValidateSource(sourceKind, sourceDocumentNo, salesOrderId, pmsProjectId);
        if (salesOrder is not null && salesOrder.ProductId != productId) throw new InvalidOperationException("制造商品必须与销售订单商品一致。");
        ValidateWorkCenter(workCenterId);
        var item = new MomWorkOrder(workOrderNo, productId, plannedStart, plannedEnd, plannedQuantity, sourceKind, sourceDocumentNo, salesOrderId, pmsProjectId, otherInfo);
        if (workCenterId is Guid selectedWorkCenter) item.SetWorkCenter(selectedWorkCenter);
        if (repository.List().Any(x => x.WorkOrderNo.Equals(item.WorkOrderNo, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("制造工单号已存在。");
        if (sourceKind != MomWorkOrderSourceKind.Manual && repository.List().Any(x => x.Status != MomWorkOrderStatus.Cancelled && x.SourceKind == sourceKind && string.Equals(x.SourceDocumentNo, item.SourceDocumentNo, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("该来源单据已生成有效制造工单。");
        repository.Add(item); return item;
    }

    public void SetStatus(MomWorkOrder item, MomWorkOrderStatus status) { item.SetStatus(status); repository.Update(item); }
    public void SetWorkCenter(MomWorkOrder item, Guid workCenterId) { ValidateWorkCenter(workCenterId); item.SetWorkCenter(workCenterId); repository.Update(item); }
    public void SetCompletedQuantity(MomWorkOrder item, decimal quantity) { item.SetCompletedQuantity(quantity); repository.Update(item); }
    public void Complete(MomWorkOrder item, decimal completedQuantity) { operationCompletionGate?.EnsureWorkOrderCanComplete(item.Id); qualityInspectionGate?.EnsureWorkOrderCanComplete(item.Id); item.SetCompletedQuantity(completedQuantity); item.SetStatus(MomWorkOrderStatus.Completed); repository.Update(item); }

    private void ValidateWorkCenter(Guid? workCenterId)
    {
        if (workCenterId is not Guid selected) return;
        var workCenter = workCenterRepository?.List().FirstOrDefault(x => x.Id == selected) ?? throw new InvalidOperationException("工作中心不存在。");
        if (workCenter.Status != MomMasterDataStatus.Active) throw new InvalidOperationException("工作中心已停用，不能绑定制造工单。");
        var factory = factoryRepository?.List().FirstOrDefault(x => x.Id == workCenter.FactoryId) ?? throw new InvalidOperationException("工作中心所属工厂不存在。");
        if (factory.Status != MomMasterDataStatus.Active) throw new InvalidOperationException("工作中心所属工厂已停用，不能绑定制造工单。");
    }

    private SalesOrder? ValidateSource(MomWorkOrderSourceKind sourceKind, string? sourceDocumentNo, Guid? salesOrderId, Guid? pmsProjectId)
    {
        if (sourceKind == MomWorkOrderSourceKind.SalesOrder)
        {
            var order = salesOrderRepository?.List().FirstOrDefault(x => x.Id == salesOrderId) ?? throw new InvalidOperationException("来源销售订单不存在。");
            if (order.Status == SalesOrderStatus.Cancelled) throw new InvalidOperationException("已取消销售订单不能生成制造工单。");
            if (!string.Equals(sourceDocumentNo, order.OrderNo, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("来源单号必须与销售订单号一致。");
            return order;
        }
        if (sourceKind == MomWorkOrderSourceKind.PmsProject)
        {
            var project = pmsProjectRepository?.List().FirstOrDefault(x => x.Id == pmsProjectId) ?? throw new InvalidOperationException("来源 PMS 项目不存在。");
            if (project.Status == PmsProjectStatus.Cancelled) throw new InvalidOperationException("已取消 PMS 项目不能生成制造工单。");
        }
        if (sourceKind == MomWorkOrderSourceKind.Planning && string.IsNullOrWhiteSpace(sourceDocumentNo)) throw new InvalidOperationException("计划来源必须填写计划单号。");
        return null;
    }
}
