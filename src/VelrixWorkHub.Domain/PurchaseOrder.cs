namespace VelrixWorkHub.Domain;

// Keep Closed appended to preserve persisted values of existing statuses.
public enum PurchaseOrderStatus { Draft, Submitted, Received, Cancelled, Closed }
public enum PurchaseOrderSourceKind { Manual, Requisition, Contract, Planning, ReorderPoint, Sourcing }

public sealed class PurchaseOrder
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string OrderNo { get; private set; } = string.Empty;
    public Guid SupplierId { get; private set; }
    public Guid ProductId { get; private set; }
    public DateOnly OrderDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public PurchaseOrderStatus Status { get; private set; }
    public PurchaseOrderSourceKind SourceKind { get; private set; }
    public string? SourceDocumentNo { get; private set; }
    public Guid? SourceLineId { get; private set; }
    public bool IsLocked { get; private set; }
    public decimal Amount => decimal.Round(Quantity * UnitPrice, 2);

    public PurchaseOrder(string orderNo, Guid supplierId, Guid productId, DateOnly orderDate, decimal quantity, decimal unitPrice, PurchaseOrderSourceKind sourceKind = PurchaseOrderSourceKind.Manual, string? sourceDocumentNo = null, DateOnly? dueDate = null, Guid? sourceLineId = null)
    {
        if (string.IsNullOrWhiteSpace(orderNo)) throw new ArgumentException("采购订单号不能为空。", nameof(orderNo));
        if (supplierId == Guid.Empty) throw new ArgumentException("必须选择供应商。", nameof(supplierId));
        if (productId == Guid.Empty) throw new ArgumentException("必须选择商品。", nameof(productId));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "采购数量必须大于 0。");
        if (unitPrice < 0) throw new ArgumentOutOfRangeException(nameof(unitPrice), "采购单价不能为负数。");
        if (sourceKind != PurchaseOrderSourceKind.Manual && string.IsNullOrWhiteSpace(sourceDocumentNo)) throw new ArgumentException("非手工采购订单必须填写来源单号。", nameof(sourceDocumentNo));
        var resolvedDueDate = dueDate ?? orderDate.AddDays(30); if (resolvedDueDate < orderDate) throw new ArgumentException("付款到期日不能早于订单日期。", nameof(dueDate));
        OrderNo = orderNo.Trim(); SupplierId = supplierId; ProductId = productId; OrderDate = orderDate; DueDate = resolvedDueDate; Quantity = quantity; UnitPrice = unitPrice; SourceKind = sourceKind; SourceDocumentNo = string.IsNullOrWhiteSpace(sourceDocumentNo) ? null : sourceDocumentNo.Trim(); SourceLineId = sourceLineId; Status = PurchaseOrderStatus.Draft;
    }

    public static PurchaseOrder Restore(Guid id, string orderNo, Guid supplierId, Guid productId, DateOnly orderDate, decimal quantity, decimal unitPrice, PurchaseOrderStatus status, PurchaseOrderSourceKind sourceKind = PurchaseOrderSourceKind.Manual, string? sourceDocumentNo = null, bool isLocked = false, DateOnly? dueDate = null, Guid? sourceLineId = null)
    {
        var item = new PurchaseOrder(orderNo, supplierId, productId, orderDate, quantity, unitPrice, sourceKind, sourceDocumentNo, dueDate, sourceLineId);
        item.Id = id;
        item.Status = status;
        item.IsLocked = isLocked;
        return item;
    }

    public void SetStatus(PurchaseOrderStatus status)
    {
        if (status == Status) return;
        if (IsLocked) throw new InvalidOperationException("采购订单已锁定，不能推进或取消订单。");

        var allowed = (Status, status) switch
        {
            (PurchaseOrderStatus.Draft, PurchaseOrderStatus.Submitted) => true,
            (PurchaseOrderStatus.Draft, PurchaseOrderStatus.Cancelled) => true,
            (PurchaseOrderStatus.Submitted, PurchaseOrderStatus.Received) => true,
            (PurchaseOrderStatus.Submitted, PurchaseOrderStatus.Cancelled) => true,
            (PurchaseOrderStatus.Received, PurchaseOrderStatus.Closed) => true,
            (PurchaseOrderStatus.Closed, PurchaseOrderStatus.Received) => true,
            _ => false
        };

        if (!allowed) throw new InvalidOperationException($"采购订单不能从“{Status}”变更为“{status}”。");
        Status = status;
    }

    /// <summary>仅供持久化事务失败恢复使用；普通状态变更必须走领域状态机。</summary>
    public void SetStatusForRecovery(PurchaseOrderStatus status) => Status = status;

    public void SetLocked(bool locked)
    {
        if (locked && Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Submitted)) throw new InvalidOperationException("只有草稿或已提交采购订单可以锁定。");
        IsLocked = locked;
    }
}
