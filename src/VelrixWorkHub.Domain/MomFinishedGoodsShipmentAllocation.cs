namespace VelrixWorkHub.Domain;

/// <summary>
/// Immutable source allocation for a finished-goods shipment.
/// A single shipment may consume multiple completed-goods receipts; each allocation
/// keeps the inventory snapshot and outbound source number independently traceable.
/// </summary>
public sealed class MomFinishedGoodsShipmentAllocation
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid ShipmentId { get; private set; }
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

    public MomFinishedGoodsShipmentAllocation(Guid shipmentId, Guid finishedGoodsReceiptId, Guid productId,
        Guid warehouseId, Guid? locationId, decimal quantity, string sourceNo, DateOnly shipmentDate,
        string? batchNo = null, DateOnly? expiryDate = null, string? serialNo = null, string? otherInfo = null, Guid? id = null)
    {
        if (shipmentId == Guid.Empty) throw new ArgumentException("发运单不能为空。", nameof(shipmentId));
        if (finishedGoodsReceiptId == Guid.Empty) throw new ArgumentException("完工入库记录不能为空。", nameof(finishedGoodsReceiptId));
        if (productId == Guid.Empty) throw new ArgumentException("发运商品不能为空。", nameof(productId));
        if (warehouseId == Guid.Empty) throw new ArgumentException("发运仓库不能为空。", nameof(warehouseId));
        if (locationId == Guid.Empty) throw new ArgumentException("发运库位无效。", nameof(locationId));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "来源分配数量必须大于零。");
        if (string.IsNullOrWhiteSpace(sourceNo)) throw new ArgumentException("来源分配单号不能为空。", nameof(sourceNo));
        if (sourceNo.Trim().Length > 80) throw new ArgumentException("来源分配单号最多 80 个字符。", nameof(sourceNo));
        if (expiryDate is DateOnly expiry && expiry < shipmentDate) throw new ArgumentException("发运保质期不能早于发运日期。", nameof(expiryDate));
        var normalizedSerial = Clean(serialNo);
        if (normalizedSerial is not null && decimal.Round(quantity, 6, MidpointRounding.AwayFromZero) != 1m)
            throw new ArgumentOutOfRangeException(nameof(quantity), "带序列号的来源分配数量必须为 1。");

        Id = id ?? Guid.CreateVersion7();
        ShipmentId = shipmentId;
        FinishedGoodsReceiptId = finishedGoodsReceiptId;
        ProductId = productId;
        WarehouseId = warehouseId;
        LocationId = locationId;
        Quantity = decimal.Round(quantity, 6, MidpointRounding.AwayFromZero);
        SourceNo = sourceNo.Trim();
        ShipmentDate = shipmentDate;
        BatchNo = Clean(batchNo);
        ExpiryDate = expiryDate;
        SerialNo = normalizedSerial;
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public static MomFinishedGoodsShipmentAllocation Restore(Guid id, Guid shipmentId, Guid finishedGoodsReceiptId,
        Guid productId, Guid warehouseId, Guid? locationId, decimal quantity, string sourceNo, DateOnly shipmentDate,
        string? batchNo, DateOnly? expiryDate, string? serialNo, string? otherInfo)
        => new(id: id, shipmentId: shipmentId, finishedGoodsReceiptId: finishedGoodsReceiptId, productId: productId,
            warehouseId: warehouseId, locationId: locationId, quantity: quantity, sourceNo: sourceNo,
            shipmentDate: shipmentDate, batchNo: batchNo, expiryDate: expiryDate, serialNo: serialNo, otherInfo: otherInfo);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
