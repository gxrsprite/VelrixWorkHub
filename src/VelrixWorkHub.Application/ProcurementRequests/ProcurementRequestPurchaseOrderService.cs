using VelrixWorkHub.Application.PurchaseOrders;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.ProcurementRequests;

public sealed class ProcurementRequestPurchaseOrderService(
    ProcurementRequestService procurementRequests,
    PurchaseOrderService purchaseOrders,
    ProcurementBudgetService? budgets = null,
    IWorkflowTransactionBoundary? transactions = null)
{
    public PurchaseOrder CreateFromApprovedRequest(Guid requestId, string orderNo, Guid supplierId, decimal unitPrice, DateOnly dueDate)
    {
        var request = procurementRequests.Get(requestId) ?? throw new InvalidOperationException("采购申请不存在或已被删除。");
        if (request.Status != OaProcurementRequestStatus.Approved) throw new InvalidOperationException("只有已批准采购申请可以生成采购订单。");

        var lines = procurementRequests.ListLines(request.Id);
        if (request.RequestType != OaProcurementRequestType.ProductRelated || lines.Count != 1 || lines[0].ProductId is not Guid productId)
            throw new InvalidOperationException("只有包含一条产品明细的已批准产品相关采购申请可以直接生成采购订单。");

        var orderDate = DateOnly.FromDateTime(DateTime.Today);
        if (dueDate < orderDate) throw new InvalidOperationException("付款到期日不能早于采购订单日期。");

        var line = lines[0];
        budgets?.PrepareForOrder(request);
        PurchaseOrder? created = null;
        void Core()
        {
            created = purchaseOrders.Create(orderNo, supplierId, productId, orderDate, line.Quantity, unitPrice,
                PurchaseOrderSourceKind.Requisition, request.DocumentNo, dueDate);
            budgets?.ConsumeForOrder(request);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core);
        return created!;
    }

    public IReadOnlyList<PurchaseOrder> CreateSplitOrdersFromApprovedRequest(Guid requestId, string orderNoPrefix, Guid supplierId,
        IReadOnlyDictionary<Guid, decimal> unitPrices, DateOnly dueDate)
    {
        var request = procurementRequests.Get(requestId) ?? throw new InvalidOperationException("采购申请不存在或已被删除。");
        if (request.Status != OaProcurementRequestStatus.Approved) throw new InvalidOperationException("只有已批准采购申请可以生成采购订单。");
        if (request.RequestType != OaProcurementRequestType.ProductRelated) throw new InvalidOperationException("只有产品相关采购申请可以拆分生成采购订单。");
        var lines = procurementRequests.ListLines(request.Id);
        if (lines.Count < 2 || lines.Any(x => x.ProductId is null)) throw new InvalidOperationException("多明细拆单至少需要两条且每条都必须绑定产品的采购明细。");
        var orderDate = DateOnly.FromDateTime(DateTime.Today);
        if (dueDate < orderDate) throw new InvalidOperationException("付款到期日不能早于采购订单日期。");
        if (string.IsNullOrWhiteSpace(orderNoPrefix)) throw new ArgumentException("采购订单号前缀不能为空。", nameof(orderNoPrefix));
        foreach (var line in lines)
            if (!unitPrices.TryGetValue(line.Id, out var unitPrice)) throw new InvalidOperationException($"采购明细 {line.ItemName} 缺少实际采购单价。");
            else if (unitPrice < 0) throw new ArgumentOutOfRangeException(nameof(unitPrices), "实际采购单价不能为负数。");

        var existingOrders = purchaseOrders.List(sourceKind: PurchaseOrderSourceKind.Requisition);
        if (lines.Any(line => existingOrders.Any(order => order.Status != PurchaseOrderStatus.Cancelled
            && string.Equals(order.SourceDocumentNo, request.DocumentNo, StringComparison.OrdinalIgnoreCase)
            && order.SourceLineId == line.Id)))
            throw new InvalidOperationException("来源明细已生成采购订单，不能重复生单；如需重试请先取消原采购订单。");

        budgets?.PrepareForOrder(request);
        var created = new List<PurchaseOrder>(lines.Count);
        void Core()
        {
            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                var productId = line.ProductId!.Value;
                var orderNo = $"{orderNoPrefix.Trim()}-{index + 1:00}";
                created.Add(purchaseOrders.Create(orderNo, supplierId, productId, orderDate, line.Quantity, unitPrices[line.Id],
                    PurchaseOrderSourceKind.Requisition, request.DocumentNo, dueDate, line.Id));
            }
            budgets?.ConsumeForOrder(request);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core);
        return created;
    }
}
