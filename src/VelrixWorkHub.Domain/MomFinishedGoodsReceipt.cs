namespace VelrixWorkHub.Domain;

/// <summary>
/// MOM 完工入库事实。库存余额仍由 ERP InventoryTransaction 计算，本记录保存制造工单到库存流水的来源追溯。
/// </summary>
public sealed class MomFinishedGoodsReceipt
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid WorkOrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid? LocationId { get; private set; }
    public decimal Quantity { get; private set; }
    public string SourceNo { get; private set; } = string.Empty;
    public DateOnly ReceiptDate { get; private set; }
    public string? BatchNo { get; private set; }
    public DateOnly? ExpiryDate { get; private set; }
    public string? SerialNo { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomFinishedGoodsReceipt(Guid workOrderId, Guid productId, Guid warehouseId, Guid? locationId,
        decimal quantity, string sourceNo, DateOnly receiptDate, string? batchNo = null, DateOnly? expiryDate = null,
        string? serialNo = null, string? otherInfo = null, Guid? id = null)
    {
        if (workOrderId == Guid.Empty) throw new ArgumentException("制造工单不能为空。", nameof(workOrderId));
        if (productId == Guid.Empty) throw new ArgumentException("完工入库商品不能为空。", nameof(productId));
        if (warehouseId == Guid.Empty) throw new ArgumentException("完工入库仓库不能为空。", nameof(warehouseId));
        if (locationId == Guid.Empty) throw new ArgumentException("完工入库库位无效。", nameof(locationId));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "完工入库数量必须大于零。");
        if (string.IsNullOrWhiteSpace(sourceNo)) throw new ArgumentException("完工入库来源单号不能为空。", nameof(sourceNo));
        if (sourceNo.Trim().Length > 80) throw new ArgumentException("完工入库来源单号最多 80 个字符。", nameof(sourceNo));
        if (expiryDate is DateOnly expiry && expiry < receiptDate) throw new ArgumentException("完工入库保质期不能早于入库日期。", nameof(expiryDate));
        var normalizedSerial = Clean(serialNo);
        if (normalizedSerial is not null && quantity != 1m) throw new ArgumentOutOfRangeException(nameof(quantity), "带序列号的完工入库数量必须为 1。");
        Id = id ?? Guid.CreateVersion7(); WorkOrderId = workOrderId; ProductId = productId; WarehouseId = warehouseId; LocationId = locationId;
        Quantity = decimal.Round(quantity, 6, MidpointRounding.AwayFromZero); SourceNo = sourceNo.Trim(); ReceiptDate = receiptDate;
        BatchNo = Clean(batchNo); ExpiryDate = expiryDate; SerialNo = normalizedSerial; OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public static MomFinishedGoodsReceipt Restore(Guid id, Guid workOrderId, Guid productId, Guid warehouseId, Guid? locationId,
        decimal quantity, string sourceNo, DateOnly receiptDate, string? batchNo, DateOnly? expiryDate, string? serialNo, string? otherInfo)
        => new(workOrderId, productId, warehouseId, locationId, quantity, sourceNo, receiptDate, batchNo, expiryDate, serialNo, otherInfo, id);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
