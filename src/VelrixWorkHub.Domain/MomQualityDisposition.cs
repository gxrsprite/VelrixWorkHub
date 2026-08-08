namespace VelrixWorkHub.Domain;

public enum MomQualityNonconformanceSeverity { Minor, Major, Critical }
public enum MomQualityNonconformanceStatus { Open, DispositionPlanned, Closed }
public enum MomQualityDispositionAction { Rework, Scrap, UseAsIs, ReturnToSupplier }
public enum MomQualityDispositionStatus { Planned, Completed, Cancelled }

/// <summary>质量检验不通过后的不可变事实入口；一条失败检验在首版只能建立一条不合格记录。</summary>
public sealed class MomQualityNonconformance
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid InspectionId { get; private set; }
    public Guid WorkOrderId { get; private set; }
    public Guid? OperationId { get; private set; }
    public Guid? ProductId { get; private set; }
    public string? BatchNo { get; private set; }
    public string NonconformanceNo { get; private set; } = string.Empty;
    public string DefectCode { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public MomQualityNonconformanceSeverity Severity { get; private set; }
    public MomQualityNonconformanceStatus Status { get; private set; }
    public Guid? DispositionId { get; private set; }
    public DateTime CreatedOn { get; private set; }
    public DateTime? ClosedOn { get; private set; }
    public string? ClosedBy { get; private set; }
    public string? ClosureNotes { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomQualityNonconformance(Guid inspectionId, Guid workOrderId, Guid? operationId, Guid? productId, string? batchNo,
        string defectCode, string description, decimal quantity, MomQualityNonconformanceSeverity severity, DateTime createdOn,
        string? otherInfo = null, Guid? id = null, string? nonconformanceNo = null)
    {
        Validate(inspectionId, workOrderId, operationId, productId, defectCode, description, quantity, severity);
        Id = id ?? Guid.CreateVersion7(); InspectionId = inspectionId; WorkOrderId = workOrderId; OperationId = operationId; ProductId = productId;
        BatchNo = Clean(batchNo); DefectCode = defectCode.Trim(); Description = description.Trim(); Quantity = Round(quantity); Severity = severity;
        CreatedOn = createdOn; NonconformanceNo = string.IsNullOrWhiteSpace(nonconformanceNo) ? BuildNonconformanceNo(Id) : nonconformanceNo.Trim();
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo)); Status = MomQualityNonconformanceStatus.Open;
    }

    public static MomQualityNonconformance Restore(Guid id, Guid inspectionId, Guid workOrderId, Guid? operationId, Guid? productId,
        string? batchNo, string defectCode, string description, decimal quantity, MomQualityNonconformanceSeverity severity,
        MomQualityNonconformanceStatus status, Guid? dispositionId, DateTime createdOn, DateTime? closedOn, string? closedBy,
        string? closureNotes, string? otherInfo, string nonconformanceNo)
    {
        var item = new MomQualityNonconformance(inspectionId, workOrderId, operationId, productId, batchNo, defectCode, description,
            quantity, severity, createdOn, otherInfo, id, nonconformanceNo);
        item.RestoreState(status, dispositionId, closedOn, closedBy, closureNotes);
        return item;
    }

    public void AssignDisposition(Guid dispositionId)
    {
        if (Status != MomQualityNonconformanceStatus.Open) throw new InvalidOperationException("只有未处置的不合格记录可以建立处置方案。");
        if (dispositionId == Guid.Empty) throw new ArgumentException("不合格处置引用无效。", nameof(dispositionId));
        DispositionId = dispositionId; Status = MomQualityNonconformanceStatus.DispositionPlanned;
    }

    public void Close(string actor, DateTime closedOn, string? notes = null)
    {
        if (Status != MomQualityNonconformanceStatus.DispositionPlanned) throw new InvalidOperationException("只有已有处置方案的不合格记录可以关闭。");
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("不合格关闭人不能为空。", nameof(actor));
        Status = MomQualityNonconformanceStatus.Closed; ClosedOn = closedOn; ClosedBy = actor.Trim(); ClosureNotes = Clean(notes);
    }

    public void RestoreState(MomQualityNonconformanceStatus status, Guid? dispositionId, DateTime? closedOn, string? closedBy, string? closureNotes)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status), "不合格状态无效。");
        if (status == MomQualityNonconformanceStatus.Open && (dispositionId is not null || closedOn is not null || closedBy is not null))
            throw new InvalidOperationException("未处置的不合格记录不能带有处置或关闭信息。");
        if (status == MomQualityNonconformanceStatus.DispositionPlanned && dispositionId is null)
            throw new InvalidOperationException("已有处置方案的不合格记录必须绑定处置引用。");
        if (status == MomQualityNonconformanceStatus.Closed && (dispositionId is null || closedOn is null || string.IsNullOrWhiteSpace(closedBy)))
            throw new InvalidOperationException("已关闭的不合格记录必须保存处置和关闭信息。");
        Status = status; DispositionId = dispositionId; ClosedOn = closedOn; ClosedBy = Clean(closedBy); ClosureNotes = Clean(closureNotes);
    }

    public static string BuildNonconformanceNo(Guid id) => $"NCR-{id:N}";

    private static void Validate(Guid inspectionId, Guid workOrderId, Guid? operationId, Guid? productId, string defectCode,
        string description, decimal quantity, MomQualityNonconformanceSeverity severity)
    {
        if (inspectionId == Guid.Empty) throw new ArgumentException("不合格记录必须绑定质量检验。", nameof(inspectionId));
        if (workOrderId == Guid.Empty) throw new ArgumentException("不合格记录必须绑定制造工单。", nameof(workOrderId));
        if (operationId == Guid.Empty || productId == Guid.Empty) throw new ArgumentException("不合格记录引用无效。", nameof(operationId));
        if (string.IsNullOrWhiteSpace(defectCode)) throw new ArgumentException("不合格编码不能为空。", nameof(defectCode));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("不合格描述不能为空。", nameof(description));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "不合格数量必须大于零。");
        if (!Enum.IsDefined(severity)) throw new ArgumentOutOfRangeException(nameof(severity), "不合格等级无效。");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static decimal Round(decimal value) => decimal.Round(value, 6, MidpointRounding.AwayFromZero);
}

/// <summary>不合格处置方案；返工必须绑定目标工单和工序，但首版不伪造库存或报工流水。</summary>
public sealed class MomQualityDisposition
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid NonconformanceId { get; private set; }
    public MomQualityDispositionAction Action { get; private set; }
    public decimal Quantity { get; private set; }
    public Guid? TargetWorkOrderId { get; private set; }
    public Guid? TargetOperationId { get; private set; }
    public string SourceNo { get; private set; } = string.Empty;
    public MomQualityDispositionStatus Status { get; private set; }
    public DateTime CreatedOn { get; private set; }
    public DateTime? CompletedOn { get; private set; }
    public string? CompletedBy { get; private set; }
    public string? Notes { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomQualityDisposition(Guid nonconformanceId, MomQualityDispositionAction action, decimal quantity,
        Guid? targetWorkOrderId, Guid? targetOperationId, DateTime createdOn, string? notes = null, string? otherInfo = null,
        Guid? id = null, string? sourceNo = null)
    {
        Validate(nonconformanceId, action, quantity, targetWorkOrderId, targetOperationId);
        Id = id ?? Guid.CreateVersion7(); NonconformanceId = nonconformanceId; Action = action; Quantity = decimal.Round(quantity, 6, MidpointRounding.AwayFromZero);
        TargetWorkOrderId = targetWorkOrderId; TargetOperationId = targetOperationId; CreatedOn = createdOn;
        SourceNo = string.IsNullOrWhiteSpace(sourceNo) ? BuildSourceNo(Id) : sourceNo.Trim(); Notes = Clean(notes); OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
        Status = MomQualityDispositionStatus.Planned;
    }

    public static MomQualityDisposition Restore(Guid id, Guid nonconformanceId, MomQualityDispositionAction action, decimal quantity,
        Guid? targetWorkOrderId, Guid? targetOperationId, string sourceNo, MomQualityDispositionStatus status, DateTime createdOn,
        DateTime? completedOn, string? completedBy, string? notes, string? otherInfo)
    {
        var item = new MomQualityDisposition(nonconformanceId, action, quantity, targetWorkOrderId, targetOperationId, createdOn, notes, otherInfo, id, sourceNo);
        item.RestoreState(status, completedOn, completedBy);
        return item;
    }

    public void Complete(string actor, DateTime completedOn, string? notes = null)
    {
        if (Status != MomQualityDispositionStatus.Planned) throw new InvalidOperationException("只有计划中的不合格处置可以完成。");
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("不合格处置完成人不能为空。", nameof(actor));
        Status = MomQualityDispositionStatus.Completed; CompletedOn = completedOn; CompletedBy = actor.Trim(); Notes = Clean(notes) ?? Notes;
    }

    public void Cancel()
    {
        if (Status != MomQualityDispositionStatus.Planned) throw new InvalidOperationException("只有计划中的不合格处置可以取消。");
        Status = MomQualityDispositionStatus.Cancelled;
    }

    public void RestoreState(MomQualityDispositionStatus status, DateTime? completedOn, string? completedBy)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status), "不合格处置状态无效。");
        if (status == MomQualityDispositionStatus.Planned && (completedOn is not null || completedBy is not null))
            throw new InvalidOperationException("计划中的不合格处置不能带有完成信息。");
        if (status == MomQualityDispositionStatus.Completed && (completedOn is null || string.IsNullOrWhiteSpace(completedBy)))
            throw new InvalidOperationException("已完成的不合格处置必须保存完成信息。");
        Status = status; CompletedOn = completedOn; CompletedBy = Clean(completedBy);
    }

    public static string BuildSourceNo(Guid id) => $"MQD-{id:N}";

    private static void Validate(Guid nonconformanceId, MomQualityDispositionAction action, decimal quantity,
        Guid? targetWorkOrderId, Guid? targetOperationId)
    {
        if (nonconformanceId == Guid.Empty) throw new ArgumentException("不合格处置必须绑定不合格记录。", nameof(nonconformanceId));
        if (!Enum.IsDefined(action)) throw new ArgumentOutOfRangeException(nameof(action), "不合格处置类型无效。");
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "处置数量必须大于零。");
        if (action == MomQualityDispositionAction.Rework && (!targetWorkOrderId.HasValue || !targetOperationId.HasValue || targetWorkOrderId == Guid.Empty || targetOperationId == Guid.Empty))
            throw new ArgumentException("返工处置必须绑定目标工单和工序。", nameof(targetOperationId));
        if (action != MomQualityDispositionAction.Rework && (targetWorkOrderId is not null || targetOperationId is not null))
            throw new ArgumentException("非返工处置不能绑定返工工单和工序。", nameof(targetWorkOrderId));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
