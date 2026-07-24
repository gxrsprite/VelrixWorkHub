namespace VelrixWorkHub.Domain;

public enum OaVehicleMaintenanceStatus
{
    Open,
    Completed,
    Cancelled
}

/// <summary>车辆维修或保养记录。记录处于 Open 时车辆保持维修中。</summary>
public sealed class OaVehicleMaintenance
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid VehicleId { get; init; }
    public Guid ReporterUserId { get; init; }
    public string ReporterName { get; private set; } = string.Empty;
    public DateTime StartedAt { get; private set; }
    public decimal? Mileage { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string? ServiceProvider { get; private set; }
    public decimal? Cost { get; private set; }
    public string OtherInfo { get; private set; } = "{}";
    public OaVehicleMaintenanceStatus Status { get; private set; }
    public string? CompletionNotes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public OaVehicleMaintenance(Guid vehicleId, Guid reporterUserId, string reporterName, DateTime startedAt, decimal? mileage,
        string description, string? serviceProvider, decimal? cost, string? otherInfo, DateTime createdAt)
    {
        if (vehicleId == Guid.Empty) throw new ArgumentException("车辆不能为空。", nameof(vehicleId));
        if (reporterUserId == Guid.Empty) throw new ArgumentException("登记人不能为空。", nameof(reporterUserId));
        VehicleId = vehicleId;
        ReporterUserId = reporterUserId;
        CreatedAt = createdAt;
        Edit(reporterName, startedAt, mileage, description, serviceProvider, cost, otherInfo);
        Status = OaVehicleMaintenanceStatus.Open;
    }

    public void Edit(string reporterName, DateTime startedAt, decimal? mileage, string description, string? serviceProvider, decimal? cost, string? otherInfo)
    {
        if (Status != OaVehicleMaintenanceStatus.Open) throw new InvalidOperationException("已完成或已取消的维修记录不能编辑。");
        ReporterName = Required(reporterName, "登记人");
        if (startedAt == default) throw new ArgumentException("开始时间不能为空。", nameof(startedAt));
        if (mileage is < 0) throw new ArgumentOutOfRangeException(nameof(mileage), "维修里程不能为负数。");
        if (cost is < 0) throw new ArgumentOutOfRangeException(nameof(cost), "维修费用不能为负数。");
        StartedAt = startedAt;
        Mileage = mileage is null ? null : decimal.Round(mileage.Value, 2);
        Description = Required(description, "维修内容");
        ServiceProvider = string.IsNullOrWhiteSpace(serviceProvider) ? null : serviceProvider.Trim();
        Cost = cost is null ? null : decimal.Round(cost.Value, 2);
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public void Complete(string notes, DateTime completedAt)
    {
        if (Status != OaVehicleMaintenanceStatus.Open) throw new InvalidOperationException("只有进行中的维修记录可以完成。");
        CompletionNotes = Required(notes, "完成说明");
        CompletedAt = completedAt;
        Status = OaVehicleMaintenanceStatus.Completed;
    }

    public void Cancel(string? notes)
    {
        if (Status != OaVehicleMaintenanceStatus.Open) throw new InvalidOperationException("只有进行中的维修记录可以取消。");
        CompletionNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Status = OaVehicleMaintenanceStatus.Cancelled;
    }

    /// <summary>仅供事务失败恢复或持久化重建使用。</summary>
    public void SetStatus(OaVehicleMaintenanceStatus status) => Status = status;
    /// <summary>仅供事务失败恢复使用。</summary>
    public void SetCompletionData(string? notes, DateTime? completedAt) { CompletionNotes = notes; CompletedAt = completedAt; }

    private static string Required(string? value, string label) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{label}不能为空。") : value.Trim();
}
