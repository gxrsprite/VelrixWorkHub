namespace VelrixWorkHub.Domain;

public enum InventoryTransactionKind { Inbound, Outbound, Adjustment }

public sealed class InventoryTransaction
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid? LocationId { get; private set; }
    public InventoryTransactionKind Kind { get; private set; }
    public decimal Quantity { get; private set; }
    public string SourceNo { get; private set; }
    public DateOnly OccurredOn { get; private set; }
    public string? Notes { get; private set; }
    public string? BatchNo { get; private set; }
    public DateOnly? ExpiryDate { get; private set; }
    public decimal SignedQuantity => Kind switch { InventoryTransactionKind.Inbound => Quantity, InventoryTransactionKind.Outbound => -Quantity, _ => Quantity };

    public InventoryTransaction(Guid productId, Guid warehouseId, InventoryTransactionKind kind, decimal quantity, string sourceNo, DateOnly occurredOn, string? notes, Guid? locationId = null, string? batchNo = null, DateOnly? expiryDate = null)
    {
        if (productId == Guid.Empty) throw new ArgumentException("必须选择商品。", nameof(productId));
        if (warehouseId == Guid.Empty) throw new ArgumentException("必须选择仓库。", nameof(warehouseId));
        if (kind == InventoryTransactionKind.Adjustment ? quantity == 0 : quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "流水数量不能为零。" );
        if (string.IsNullOrWhiteSpace(sourceNo)) throw new ArgumentException("流水单号不能为空。", nameof(sourceNo));
        if (expiryDate is DateOnly expiry && expiry < occurredOn) throw new ArgumentException("保质期不能早于流水日期。", nameof(expiryDate));
        ProductId = productId; WarehouseId = warehouseId; LocationId = locationId; Kind = kind; Quantity = quantity; SourceNo = sourceNo.Trim(); OccurredOn = occurredOn; Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(); BatchNo = string.IsNullOrWhiteSpace(batchNo) ? null : batchNo.Trim(); ExpiryDate = expiryDate;
    }
}
