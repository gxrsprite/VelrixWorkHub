namespace VelrixWorkHub.Domain;

public enum MomOperationStatus { Pending, Ready, InProgress, Paused, Completed, Cancelled }

/// <summary>
/// 从已发布 BOM 工序顺序冻结的工单工序。工序状态独立于制造工单状态。
/// </summary>
public sealed class MomWorkOrderOperation
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid WorkOrderId { get; private set; }
    public int OperationSequence { get; private set; }
    public string OperationCode { get; private set; } = string.Empty;
    public string OperationName { get; private set; } = string.Empty;
    public Guid WorkCenterId { get; private set; }
    public decimal PlannedQuantity { get; private set; }
    public decimal StandardSetupHours { get; private set; }
    public decimal StandardRunHoursPerUnit { get; private set; }
    public decimal StandardHours => Round(StandardSetupHours + StandardRunHoursPerUnit * PlannedQuantity);
    public decimal ReportedQuantity { get; private set; }
    public decimal GoodQuantity { get; private set; }
    public decimal ScrapQuantity { get; private set; }
    public MomOperationStatus Status { get; private set; }
    public string? AcceptedBy { get; private set; }
    public DateTime? AcceptedOn { get; private set; }
    public DateTime? StartedOn { get; private set; }
    public DateTime? PausedOn { get; private set; }
    public DateTime? CompletedOn { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomWorkOrderOperation(Guid workOrderId, int operationSequence, string operationCode, string operationName,
        Guid workCenterId, decimal plannedQuantity, string? otherInfo = null, decimal standardSetupHours = 0, decimal standardRunHoursPerUnit = 0)
    {
        Validate(workOrderId, operationSequence, operationCode, operationName, workCenterId, plannedQuantity);
        ValidateStandardHours(standardSetupHours, standardRunHoursPerUnit);
        WorkOrderId = workOrderId; OperationSequence = operationSequence; OperationCode = operationCode.Trim(); OperationName = operationName.Trim();
        WorkCenterId = workCenterId; PlannedQuantity = Round(plannedQuantity); StandardSetupHours = Round(standardSetupHours); StandardRunHoursPerUnit = Round(standardRunHoursPerUnit); OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
        Status = MomOperationStatus.Pending;
    }

    public static MomWorkOrderOperation Restore(Guid id, Guid workOrderId, int operationSequence, string operationCode, string operationName,
        Guid workCenterId, decimal plannedQuantity, decimal reportedQuantity, decimal goodQuantity, decimal scrapQuantity,
        MomOperationStatus status, string? acceptedBy, DateTime? acceptedOn, DateTime? startedOn, DateTime? pausedOn,
        DateTime? completedOn, string? otherInfo, decimal standardSetupHours = 0, decimal standardRunHoursPerUnit = 0)
    {
        var item = new MomWorkOrderOperation(workOrderId, operationSequence, operationCode, operationName, workCenterId, plannedQuantity, otherInfo, standardSetupHours, standardRunHoursPerUnit)
        { Id = id, Status = status, AcceptedBy = Clean(acceptedBy), AcceptedOn = acceptedOn, StartedOn = startedOn, PausedOn = pausedOn, CompletedOn = completedOn };
        item.RestoreReportTotals(reportedQuantity, goodQuantity, scrapQuantity);
        if (status == MomOperationStatus.Completed && item.ReportedQuantity < item.PlannedQuantity) throw new InvalidOperationException("已完工工序的报工数量不足计划数量。");
        return item;
    }

    public void Accept(string actor, DateTime occurredOn)
    {
        EnsureActor(actor);
        if (Status != MomOperationStatus.Pending) throw new InvalidOperationException("只有待受理工序可以受理。");
        Status = MomOperationStatus.Ready; AcceptedBy = actor.Trim(); AcceptedOn = occurredOn;
    }

    public void Start(DateTime occurredOn)
    {
        if (Status != MomOperationStatus.Ready) throw new InvalidOperationException("只有已受理工序可以开始执行。");
        Status = MomOperationStatus.InProgress; StartedOn ??= occurredOn;
    }

    public void Pause(DateTime occurredOn)
    {
        if (Status != MomOperationStatus.InProgress) throw new InvalidOperationException("只有执行中的工序可以暂停。");
        Status = MomOperationStatus.Paused; PausedOn = occurredOn;
    }

    public void Resume(DateTime occurredOn)
    {
        if (Status != MomOperationStatus.Paused) throw new InvalidOperationException("只有已暂停工序可以恢复。");
        Status = MomOperationStatus.InProgress; PausedOn = null;
    }

    public void Report(decimal goodQuantity, decimal scrapQuantity)
    {
        if (Status != MomOperationStatus.InProgress) throw new InvalidOperationException("只有执行中的工序可以报工。");
        var good = Round(goodQuantity); var scrap = Round(scrapQuantity); var quantity = Round(good + scrap);
        if (good < 0 || scrap < 0 || quantity <= 0) throw new ArgumentOutOfRangeException(nameof(goodQuantity), "报工数量必须大于零，良品和不良品数量不能为负数。");
        if (quantity > RemainingQuantity) throw new InvalidOperationException("报工数量不能超过工序剩余计划数量。");
        ReportedQuantity = Round(ReportedQuantity + quantity); GoodQuantity = Round(GoodQuantity + good); ScrapQuantity = Round(ScrapQuantity + scrap);
    }

    public void Complete(DateTime occurredOn)
    {
        if (Status != MomOperationStatus.InProgress) throw new InvalidOperationException("只有执行中的工序可以完工。");
        if (ReportedQuantity < PlannedQuantity) throw new InvalidOperationException("工序报工数量未达到计划数量。");
        Status = MomOperationStatus.Completed; CompletedOn = occurredOn;
    }

    public void ReopenForCorrection()
    {
        if (Status != MomOperationStatus.Completed) throw new InvalidOperationException("只有已完工工序可以因报工更正重开。");
        Status = MomOperationStatus.InProgress; CompletedOn = null; PausedOn = null;
    }

    public decimal RemainingQuantity => Math.Max(0, PlannedQuantity - ReportedQuantity);

    /// <summary>事务失败时恢复当前执行快照，数据库回滚由宿主事务负责。</summary>
    public void RestoreReportTotals(decimal reportedQuantity, decimal goodQuantity, decimal scrapQuantity)
    {
        var reported = Round(reportedQuantity); var good = Round(goodQuantity); var scrap = Round(scrapQuantity);
        if (reported < 0 || good < 0 || scrap < 0 || reported > PlannedQuantity || Round(good + scrap) != reported)
            throw new InvalidOperationException("工序报工累计量无效。");
        ReportedQuantity = reported; GoodQuantity = good; ScrapQuantity = scrap;
    }

    public void RestoreStatus(MomOperationStatus status, DateTime? startedOn, DateTime? pausedOn, DateTime? completedOn)
    {
        Status = status; StartedOn = startedOn; PausedOn = pausedOn; CompletedOn = completedOn;
    }

    public void RestoreLifecycle(MomOperationStatus status, string? acceptedBy, DateTime? acceptedOn,
        DateTime? startedOn, DateTime? pausedOn, DateTime? completedOn)
    {
        Status = status; AcceptedBy = Clean(acceptedBy); AcceptedOn = acceptedOn; StartedOn = startedOn; PausedOn = pausedOn; CompletedOn = completedOn;
    }

    private static void Validate(Guid workOrderId, int operationSequence, string operationCode, string operationName, Guid workCenterId, decimal plannedQuantity)
    {
        if (workOrderId == Guid.Empty) throw new ArgumentException("工序必须绑定制造工单。", nameof(workOrderId));
        if (operationSequence < 0) throw new ArgumentOutOfRangeException(nameof(operationSequence), "工序顺序不能为负数。");
        if (string.IsNullOrWhiteSpace(operationCode)) throw new ArgumentException("工序编码不能为空。", nameof(operationCode));
        if (string.IsNullOrWhiteSpace(operationName)) throw new ArgumentException("工序名称不能为空。", nameof(operationName));
        if (workCenterId == Guid.Empty) throw new ArgumentException("工序必须绑定工作中心。", nameof(workCenterId));
        if (plannedQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(plannedQuantity), "工序计划数量必须大于零。");
    }

    private static void ValidateStandardHours(decimal setupHours, decimal runHoursPerUnit)
    {
        if (setupHours < 0) throw new ArgumentOutOfRangeException(nameof(setupHours), "准备工时不能为负数。");
        if (runHoursPerUnit < 0) throw new ArgumentOutOfRangeException(nameof(runHoursPerUnit), "单位运行工时不能为负数。");
    }

    private static void EnsureActor(string actor) { if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("工序操作者不能为空。", nameof(actor)); }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static decimal Round(decimal value) => decimal.Round(value, 6, MidpointRounding.AwayFromZero);
}

/// <summary>工序报工不可变记录，保存本次良品/不良品数量和操作者。</summary>
public sealed class MomWorkOrderOperationReport
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid OperationId { get; private set; }
    public Guid WorkOrderId { get; private set; }
    public Guid WorkCenterId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal GoodQuantity { get; private set; }
    public decimal ScrapQuantity { get; private set; }
    public string SourceNo { get; private set; }
    public DateTime OccurredOn { get; private set; }
    public string Actor { get; private set; }
    public string? Notes { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomWorkOrderOperationReport(Guid operationId, Guid workOrderId, Guid workCenterId, decimal goodQuantity,
        decimal scrapQuantity, string sourceNo, DateTime occurredOn, string actor, string? notes = null, string? otherInfo = null, Guid? id = null)
    {
        if (operationId == Guid.Empty) throw new ArgumentException("报工必须绑定工序。", nameof(operationId));
        if (workOrderId == Guid.Empty) throw new ArgumentException("报工必须绑定制造工单。", nameof(workOrderId));
        if (workCenterId == Guid.Empty) throw new ArgumentException("报工必须绑定工作中心。", nameof(workCenterId));
        var good = Round(goodQuantity); var scrap = Round(scrapQuantity); var quantity = Round(good + scrap);
        if (good < 0 || scrap < 0 || quantity <= 0) throw new ArgumentOutOfRangeException(nameof(goodQuantity), "报工数量必须大于零，良品和不良品数量不能为负数。");
        if (string.IsNullOrWhiteSpace(sourceNo)) throw new ArgumentException("报工流水号不能为空。", nameof(sourceNo));
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("工序操作者不能为空。", nameof(actor));
        Id = id ?? Guid.CreateVersion7(); OperationId = operationId; WorkOrderId = workOrderId; WorkCenterId = workCenterId;
        Quantity = quantity; GoodQuantity = good; ScrapQuantity = scrap; SourceNo = sourceNo.Trim(); OccurredOn = occurredOn; Actor = actor.Trim();
        Notes = Clean(notes); OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public static string BuildSourceNo(Guid operationId, Guid reportId) => $"MORP-{operationId:N}-{reportId:N}";

    public static MomWorkOrderOperationReport Restore(Guid id, Guid operationId, Guid workOrderId, Guid workCenterId,
        decimal goodQuantity, decimal scrapQuantity, string sourceNo, DateTime occurredOn, string actor, string? notes, string? otherInfo)
        => new(operationId, workOrderId, workCenterId, goodQuantity, scrapQuantity, sourceNo, occurredOn, actor, notes, otherInfo, id);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static decimal Round(decimal value) => decimal.Round(value, 6, MidpointRounding.AwayFromZero);
}
