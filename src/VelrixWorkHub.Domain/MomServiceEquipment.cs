namespace VelrixWorkHub.Domain;

public enum MomServiceEquipmentStatus
{
    PendingInstallation,
    Active,
    Retired
}

/// <summary>
/// 客户收到的成品设备售后档案。它和 LMS 客户机台保持边界：LMS 记录许可相关机台信息，MOM 记录交付后的设备身份与服务生命周期。
/// </summary>
public sealed class MomServiceEquipment
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string EquipmentNo { get; private set; } = string.Empty;
    public Guid CustomerId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid SalesOrderId { get; private set; }
    public Guid ShipmentId { get; private set; }
    public string? ShipmentSourceNo { get; private set; }
    public Guid? PmsProjectId { get; private set; }
    public string SerialNo { get; private set; } = string.Empty;
    public string? Model { get; private set; }
    public string? InstallationLocation { get; private set; }
    public string? InstalledBy { get; private set; }
    public DateOnly? InstalledOn { get; private set; }
    public DateOnly? WarrantyStartDate { get; private set; }
    public DateOnly? WarrantyEndDate { get; private set; }
    public MomServiceEquipmentStatus Status { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTime CreatedOn { get; private set; }
    public string? RetiredBy { get; private set; }
    public DateTime? RetiredOn { get; private set; }
    public string? RetiredReason { get; private set; }
    public string? Notes { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomServiceEquipment(string equipmentNo, Guid customerId, Guid productId, Guid salesOrderId, Guid shipmentId,
        string serialNo, string createdBy, DateTime? createdOn = null, string? shipmentSourceNo = null,
        Guid? pmsProjectId = null, string? model = null, DateOnly? warrantyStartDate = null,
        DateOnly? warrantyEndDate = null, string? notes = null, string? otherInfo = null, Guid? id = null)
    {
        ValidateIdentity(equipmentNo, customerId, productId, salesOrderId, shipmentId, serialNo, createdBy);
        ValidateWarranty(warrantyStartDate, warrantyEndDate);
        Id = id ?? Guid.CreateVersion7(); EquipmentNo = equipmentNo.Trim(); CustomerId = customerId; ProductId = productId;
        SalesOrderId = salesOrderId; ShipmentId = shipmentId; ShipmentSourceNo = Clean(shipmentSourceNo);
        PmsProjectId = pmsProjectId; SerialNo = serialNo.Trim(); Model = Clean(model);
        WarrantyStartDate = warrantyStartDate; WarrantyEndDate = warrantyEndDate; Notes = Clean(notes);
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo)); CreatedBy = createdBy.Trim(); CreatedOn = createdOn ?? DateTime.Now;
        Status = MomServiceEquipmentStatus.PendingInstallation;
    }

    public static MomServiceEquipment Restore(Guid id, string equipmentNo, Guid customerId, Guid productId, Guid salesOrderId,
        Guid shipmentId, string? shipmentSourceNo, Guid? pmsProjectId, string serialNo, string? model,
        string? installationLocation, string? installedBy, DateOnly? installedOn, DateOnly? warrantyStartDate,
        DateOnly? warrantyEndDate, MomServiceEquipmentStatus status, string createdBy, DateTime createdOn,
        string? retiredBy, DateTime? retiredOn, string? retiredReason, string? notes, string? otherInfo)
        => new(equipmentNo, customerId, productId, salesOrderId, shipmentId, serialNo, createdBy, createdOn, shipmentSourceNo,
            pmsProjectId, model, warrantyStartDate, warrantyEndDate, notes, otherInfo, id)
        {
            InstallationLocation = Clean(installationLocation), InstalledBy = Clean(installedBy), InstalledOn = installedOn,
            Status = status, RetiredBy = Clean(retiredBy), RetiredOn = retiredOn, RetiredReason = Clean(retiredReason)
        };

    public void Install(string actor, DateOnly installedOn, string location)
    {
        if (Status != MomServiceEquipmentStatus.PendingInstallation) throw new InvalidOperationException("只有待安装设备可以登记安装。");
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("安装人不能为空。", nameof(actor));
        if (string.IsNullOrWhiteSpace(location)) throw new ArgumentException("安装位置不能为空。", nameof(location));
        Status = MomServiceEquipmentStatus.Active; InstalledBy = actor.Trim(); InstallationLocation = location.Trim();
        this.InstalledOn = installedOn;
    }

    public void Retire(string actor, DateTime? retiredOn, string reason)
    {
        if (Status != MomServiceEquipmentStatus.Active) throw new InvalidOperationException("只有在用设备可以报废。");
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("操作人不能为空。", nameof(actor));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("报废原因不能为空。", nameof(reason));
        Status = MomServiceEquipmentStatus.Retired; RetiredBy = actor.Trim(); RetiredOn = retiredOn ?? DateTime.Now; RetiredReason = reason.Trim();
    }

    public void RestoreLifecycle(MomServiceEquipmentStatus status, string? installationLocation, string? installedBy,
        DateOnly? installedOn, string? retiredBy, DateTime? retiredOn, string? retiredReason)
    {
        Status = status; InstallationLocation = Clean(installationLocation); InstalledBy = Clean(installedBy); this.InstalledOn = installedOn;
        RetiredBy = Clean(retiredBy); RetiredOn = retiredOn; RetiredReason = Clean(retiredReason);
    }

    private static void ValidateIdentity(string equipmentNo, Guid customerId, Guid productId, Guid salesOrderId, Guid shipmentId, string serialNo, string createdBy)
    {
        if (string.IsNullOrWhiteSpace(equipmentNo)) throw new ArgumentException("设备档案编号不能为空。", nameof(equipmentNo));
        if (equipmentNo.Trim().Length > 80) throw new ArgumentException("设备档案编号最多 80 个字符。", nameof(equipmentNo));
        if (customerId == Guid.Empty) throw new ArgumentException("客户不能为空。", nameof(customerId));
        if (productId == Guid.Empty) throw new ArgumentException("商品不能为空。", nameof(productId));
        if (salesOrderId == Guid.Empty) throw new ArgumentException("销售订单不能为空。", nameof(salesOrderId));
        if (shipmentId == Guid.Empty) throw new ArgumentException("发运记录不能为空。", nameof(shipmentId));
        if (string.IsNullOrWhiteSpace(serialNo)) throw new ArgumentException("设备序列号不能为空。", nameof(serialNo));
        if (serialNo.Trim().Length > 100) throw new ArgumentException("设备序列号最多 100 个字符。", nameof(serialNo));
        if (string.IsNullOrWhiteSpace(createdBy)) throw new ArgumentException("创建人不能为空。", nameof(createdBy));
    }

    private static void ValidateWarranty(DateOnly? start, DateOnly? end)
    {
        if (end is DateOnly endDate && start is DateOnly startDate && endDate < startDate)
            throw new ArgumentException("保修结束日期不能早于开始日期。", nameof(end));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public enum MomServiceEquipmentLifecycleAction
{
    Created,
    Installed,
    Retired
}

public sealed class MomServiceEquipmentLifecycleEntry
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid EquipmentId { get; private set; }
    public MomServiceEquipmentLifecycleAction Action { get; private set; }
    public MomServiceEquipmentStatus? FromStatus { get; private set; }
    public MomServiceEquipmentStatus ToStatus { get; private set; }
    public string Actor { get; private set; } = string.Empty;
    public DateTime OccurredOn { get; private set; }
    public string? Reason { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomServiceEquipmentLifecycleEntry(Guid equipmentId, MomServiceEquipmentLifecycleAction action,
        MomServiceEquipmentStatus? fromStatus, MomServiceEquipmentStatus toStatus, string actor,
        DateTime? occurredOn = null, string? reason = null, string? otherInfo = null, Guid? id = null)
    {
        if (equipmentId == Guid.Empty) throw new ArgumentException("设备档案不能为空。", nameof(equipmentId));
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("操作人不能为空。", nameof(actor));
        Id = id ?? Guid.CreateVersion7(); EquipmentId = equipmentId; Action = action; FromStatus = fromStatus; ToStatus = toStatus;
        Actor = actor.Trim(); OccurredOn = occurredOn ?? DateTime.Now; Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public static MomServiceEquipmentLifecycleEntry Restore(Guid id, Guid equipmentId, MomServiceEquipmentLifecycleAction action,
        MomServiceEquipmentStatus? fromStatus, MomServiceEquipmentStatus toStatus, string actor, DateTime occurredOn,
        string? reason, string? otherInfo)
        => new(equipmentId, action, fromStatus, toStatus, actor, occurredOn, reason, otherInfo, id);
}
