namespace VelrixWorkHub.Domain;

/// <summary>
/// 售后维修工单的备件消耗事实。库存余额仍由 ERP InventoryTransaction 计算，
/// 本记录保存维修工单、设备和库存维度的不可变追溯快照。
/// </summary>
public sealed class MomServiceWorkOrderPartConsumption
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid ServiceWorkOrderId { get; private set; }
    public Guid EquipmentId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid? LocationId { get; private set; }
    public decimal Quantity { get; private set; }
    public string SourceNo { get; private set; } = string.Empty;
    public DateOnly ConsumedOn { get; private set; }
    public string? BatchNo { get; private set; }
    public DateOnly? ExpiryDate { get; private set; }
    public string? SerialNo { get; private set; }
    public string Actor { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomServiceWorkOrderPartConsumption(Guid serviceWorkOrderId, Guid equipmentId, Guid productId,
        Guid warehouseId, Guid? locationId, decimal quantity, string sourceNo, DateOnly consumedOn,
        string? batchNo, DateOnly? expiryDate, string? serialNo, string actor, string? notes = null,
        string? otherInfo = null, Guid? id = null)
    {
        Validate(serviceWorkOrderId, equipmentId, productId, warehouseId, quantity, sourceNo, consumedOn, expiryDate, serialNo, actor);
        Id = id ?? Guid.CreateVersion7(); ServiceWorkOrderId = serviceWorkOrderId; EquipmentId = equipmentId;
        ProductId = productId; WarehouseId = warehouseId; LocationId = locationId; Quantity = Round(quantity);
        SourceNo = sourceNo.Trim(); ConsumedOn = consumedOn; BatchNo = Clean(batchNo); ExpiryDate = expiryDate;
        SerialNo = Clean(serialNo); Actor = actor.Trim(); Notes = Clean(notes); OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public static MomServiceWorkOrderPartConsumption Restore(Guid id, Guid serviceWorkOrderId, Guid equipmentId,
        Guid productId, Guid warehouseId, Guid? locationId, decimal quantity, string sourceNo, DateOnly consumedOn,
        string? batchNo, DateOnly? expiryDate, string? serialNo, string actor, string? notes, string? otherInfo)
        => new(serviceWorkOrderId, equipmentId, productId, warehouseId, locationId, quantity, sourceNo, consumedOn,
            batchNo, expiryDate, serialNo, actor, notes, otherInfo, id);

    private static void Validate(Guid serviceWorkOrderId, Guid equipmentId, Guid productId, Guid warehouseId,
        decimal quantity, string sourceNo, DateOnly consumedOn, DateOnly? expiryDate, string? serialNo, string actor)
    {
        if (serviceWorkOrderId == Guid.Empty) throw new ArgumentException("维修服务工单不能为空。", nameof(serviceWorkOrderId));
        if (equipmentId == Guid.Empty) throw new ArgumentException("维修设备不能为空。", nameof(equipmentId));
        if (productId == Guid.Empty) throw new ArgumentException("备件商品不能为空。", nameof(productId));
        if (warehouseId == Guid.Empty) throw new ArgumentException("备件仓库不能为空。", nameof(warehouseId));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "备件消耗数量必须大于零。");
        if (string.IsNullOrWhiteSpace(sourceNo)) throw new ArgumentException("备件消耗单号不能为空。", nameof(sourceNo));
        if (sourceNo.Trim().Length > 80) throw new ArgumentException("备件消耗单号最多 80 个字符。", nameof(sourceNo));
        if (expiryDate is DateOnly expiry && expiry < consumedOn) throw new ArgumentException("维修备件保质期不能早于消耗日期。 ", nameof(expiryDate));
        if (!string.IsNullOrWhiteSpace(serialNo) && decimal.Round(quantity, 2, MidpointRounding.AwayFromZero) != 1m)
            throw new ArgumentOutOfRangeException(nameof(quantity), "带序列号的维修备件数量必须为 1。 ");
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("备件消耗操作人不能为空。", nameof(actor));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
