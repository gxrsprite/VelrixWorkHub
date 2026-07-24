using VelrixWorkHub.Application.PurchaseOrders;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.ProcurementRequests;

/// <summary>将已定标寻源结果转换为 ERP 采购订单；产品和数量因寻源申请不绑定产品而由采购复核时明确输入。</summary>
public sealed class ProcurementSourcingPurchaseOrderService(
    ProcurementSourcingService sourcings,
    PurchaseOrderService purchaseOrders,
    IWorkflowTransactionBoundary? transactions = null)
{
    public PurchaseOrder? GetExistingOrder(OaProcurementSourcing sourcing)
        => purchaseOrders.List(sourceKind: PurchaseOrderSourceKind.Sourcing)
            .Where(x => x.Status != PurchaseOrderStatus.Cancelled
                && string.Equals(x.SourceDocumentNo, sourcing.SourcingNo, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.OrderDate)
            .FirstOrDefault();

    public PurchaseOrder CreateFromAwardedQuote(OaProcurementSourcing sourcing, string orderNo, Guid productId,
        decimal quantity, DateOnly dueDate, bool canManage)
    {
        if (!canManage) throw new UnauthorizedAccessException("当前用户没有将中选报价转采购订单的权限。");
        if (sourcing.Status != OaProcurementSourcingStatus.Awarded)
            throw new InvalidOperationException("只有已定标寻源单可以生成采购订单。");
        if (dueDate < DateOnly.FromDateTime(DateTime.Today)) throw new InvalidOperationException("付款到期日不能早于采购订单日期。");

        var existing = GetExistingOrder(sourcing);
        if (existing is not null) return existing;

        var quote = sourcings.GetAwardedQuote(sourcing) ?? throw new InvalidOperationException("中选报价不存在，不能生成采购订单。");
        PurchaseOrder? created = null;
        void Core()
        {
            created = purchaseOrders.Create(orderNo, quote.SupplierId, productId, DateOnly.FromDateTime(DateTime.Today), quantity,
                quote.QuoteAmount, PurchaseOrderSourceKind.Sourcing, sourcing.SourcingNo, dueDate);
        }

        if (transactions is null) Core();
        else transactions.Execute(Core);
        return created!;
    }
}
