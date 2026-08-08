namespace VelrixWorkHub.Domain;

/// <summary>
/// 工序实际工时不可变记录。员工身份和时间范围由 MOM Application 校验后写入。
/// </summary>
public sealed class MomWorkOrderOperationWorkLog
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid OperationId { get; private set; }
    public Guid WorkOrderId { get; private set; }
    public Guid WorkCenterId { get; private set; }
    public Guid OperatorUserId { get; private set; }
    public string OperatorName { get; private set; }
    public Guid? EquipmentId { get; private set; }
    public string? EquipmentName { get; private set; }
    public DateTime StartedOn { get; private set; }
    public DateTime EndedOn { get; private set; }
    public decimal Hours { get; private set; }
    public string SourceNo { get; private set; }
    public string? Notes { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomWorkOrderOperationWorkLog(Guid operationId, Guid workOrderId, Guid workCenterId, Guid operatorUserId,
        string operatorName, Guid? equipmentId, string? equipmentName, DateTime startedOn, DateTime endedOn, string sourceNo, string? notes = null,
        string? otherInfo = null, Guid? id = null)
    {
        if (operationId == Guid.Empty) throw new ArgumentException("工时记录必须绑定工序。", nameof(operationId));
        if (workOrderId == Guid.Empty) throw new ArgumentException("工时记录必须绑定制造工单。", nameof(workOrderId));
        if (workCenterId == Guid.Empty) throw new ArgumentException("工时记录必须绑定工作中心。", nameof(workCenterId));
        if (operatorUserId == Guid.Empty) throw new ArgumentException("工时记录必须绑定员工。", nameof(operatorUserId));
        if (string.IsNullOrWhiteSpace(operatorName)) throw new ArgumentException("工时记录必须填写员工。", nameof(operatorName));
        if (equipmentId is Guid selectedEquipmentId && selectedEquipmentId == Guid.Empty) throw new ArgumentException("设备引用无效。", nameof(equipmentId));
        if (equipmentId is not null && string.IsNullOrWhiteSpace(equipmentName)) throw new ArgumentException("工时记录必须保存设备名称。", nameof(equipmentName));
        if (equipmentId is null && !string.IsNullOrWhiteSpace(equipmentName)) throw new ArgumentException("没有设备引用时不能保存设备名称。", nameof(equipmentName));
        if (endedOn <= startedOn) throw new ArgumentException("工时结束时间必须晚于开始时间。", nameof(endedOn));
        if (string.IsNullOrWhiteSpace(sourceNo)) throw new ArgumentException("工时流水号不能为空。", nameof(sourceNo));

        Id = id ?? Guid.CreateVersion7(); OperationId = operationId; WorkOrderId = workOrderId; WorkCenterId = workCenterId;
        OperatorUserId = operatorUserId; OperatorName = operatorName.Trim(); EquipmentId = equipmentId; EquipmentName = Clean(equipmentName); StartedOn = startedOn; EndedOn = endedOn;
        Hours = Round((decimal)(endedOn - startedOn).TotalHours); SourceNo = sourceNo.Trim(); Notes = Clean(notes);
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public static string BuildSourceNo(Guid operationId, Guid logId) => $"MOWL-{operationId:N}-{logId:N}";

    public static MomWorkOrderOperationWorkLog Restore(Guid id, Guid operationId, Guid workOrderId, Guid workCenterId,
        Guid operatorUserId, string operatorName, Guid? equipmentId, string? equipmentName, DateTime startedOn, DateTime endedOn, decimal hours, string sourceNo,
        string? notes, string? otherInfo)
    {
        var item = new MomWorkOrderOperationWorkLog(operationId, workOrderId, workCenterId, operatorUserId, operatorName,
            equipmentId, equipmentName, startedOn, endedOn, sourceNo, notes, otherInfo, id);
        if (Round(hours) != item.Hours) throw new InvalidOperationException("工时记录时长与时间范围不一致。");
        return item;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static decimal Round(decimal value) => decimal.Round(value, 6, MidpointRounding.AwayFromZero);
}
