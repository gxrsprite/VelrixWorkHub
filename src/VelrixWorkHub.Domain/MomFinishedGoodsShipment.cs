namespace VelrixWorkHub.Domain;

/// <summary>
/// MOM finished-goods shipment trace. ERP inventory remains the stock ledger;
/// this record links one completed-goods receipt to a fully shipped sales order.
/// </summary>
public sealed class MomFinishedGoodsShipment
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid SalesOrderId { get; private set; }
    public Guid FinishedGoodsReceiptId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid? LocationId { get; private set; }
    public decimal Quantity { get; private set; }
    public string SourceNo { get; private set; } = string.Empty;
    public DateOnly ShipmentDate { get; private set; }
    public string? BatchNo { get; private set; }
    public DateOnly? ExpiryDate { get; private set; }
    public string? SerialNo { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomFinishedGoodsShipment(Guid salesOrderId, Guid finishedGoodsReceiptId, Guid productId, Guid warehouseId,
        Guid? locationId, decimal quantity, string sourceNo, DateOnly shipmentDate, string? batchNo = null,
        DateOnly? expiryDate = null, string? serialNo = null, string? otherInfo = null, Guid? id = null)
    {
        if (salesOrderId == Guid.Empty) throw new ArgumentException("销售订单不能为空。", nameof(salesOrderId));
        if (finishedGoodsReceiptId == Guid.Empty) throw new ArgumentException("完工入库记录不能为空。", nameof(finishedGoodsReceiptId));
        if (productId == Guid.Empty) throw new ArgumentException("发运商品不能为空。", nameof(productId));
        if (warehouseId == Guid.Empty) throw new ArgumentException("发运仓库不能为空。", nameof(warehouseId));
        if (locationId == Guid.Empty) throw new ArgumentException("发运库位无效。", nameof(locationId));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "发运数量必须大于零。");
        if (string.IsNullOrWhiteSpace(sourceNo)) throw new ArgumentException("发运来源单号不能为空。", nameof(sourceNo));
        if (sourceNo.Trim().Length > 80) throw new ArgumentException("发运来源单号最多 80 个字符。", nameof(sourceNo));
        if (expiryDate is DateOnly expiry && expiry < shipmentDate) throw new ArgumentException("发运保质期不能早于发运日期。", nameof(expiryDate));
        var normalizedSerial = Clean(serialNo);
        if (normalizedSerial is not null && quantity != 1m) throw new ArgumentOutOfRangeException(nameof(quantity), "带序列号的发运数量必须为 1。");

        Id = id ?? Guid.CreateVersion7(); SalesOrderId = salesOrderId; FinishedGoodsReceiptId = finishedGoodsReceiptId;
        ProductId = productId; WarehouseId = warehouseId; LocationId = locationId;
        Quantity = decimal.Round(quantity, 6, MidpointRounding.AwayFromZero); SourceNo = sourceNo.Trim(); ShipmentDate = shipmentDate;
        BatchNo = Clean(batchNo); ExpiryDate = expiryDate; SerialNo = normalizedSerial; OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public static MomFinishedGoodsShipment Restore(Guid id, Guid salesOrderId, Guid finishedGoodsReceiptId, Guid productId,
        Guid warehouseId, Guid? locationId, decimal quantity, string sourceNo, DateOnly shipmentDate, string? batchNo,
        DateOnly? expiryDate, string? serialNo, string? otherInfo)
        => new(salesOrderId, finishedGoodsReceiptId, productId, warehouseId, locationId, quantity, sourceNo, shipmentDate,
            batchNo, expiryDate, serialNo, otherInfo, id);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
