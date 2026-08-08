using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

/// <summary>
/// MOM-06A 工序执行首版。工序快照来自已发布 BOM 的 OperationSequence，状态与工单状态分离。
/// </summary>
public sealed class MomOperationExecutionService(
    IMomWorkOrderRepository workOrderRepository,
    IMomWorkOrderOperationRepository operationRepository,
    IMomWorkOrderOperationReportRepository reportRepository,
    IMomWorkOrderOperationReportCorrectionRepository correctionRepository,
    IMomWorkOrderOperationWorkLogRepository workLogRepository,
    IMomOperatorResolver operatorResolver,
    IMomEquipmentResolver equipmentResolver,
    MomMaterialKittingService materialKittingService,
    IMomManufacturingComponentRepository componentRepository,
    IMomWorkCenterRepository workCenterRepository,
    IMomManufacturingOperationStandardRepository operationStandardRepository,
    IWorkflowTransactionBoundary? transactions = null,
    IMomQualityInspectionGate? qualityInspectionGate = null) : IMomOperationCompletionGate
{
    public IReadOnlyList<MomWorkOrderOperation> List(Guid workOrderId)
        => operationRepository.List().Where(x => x.WorkOrderId == workOrderId).OrderBy(x => x.OperationSequence).ToArray();

    public IReadOnlyList<MomWorkOrderOperationReport> ListReports(Guid operationId)
        => reportRepository.List().Where(x => x.OperationId == operationId).OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.SourceNo).ToArray();

    public IReadOnlyList<MomWorkOrderOperationReportCorrection> ListCorrections(Guid operationId)
        => correctionRepository.List().Where(x => x.OperationId == operationId).OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.SourceNo).ToArray();

    public IReadOnlyList<MomWorkOrderOperationWorkLog> ListWorkLogs(Guid operationId)
        => workLogRepository.List().Where(x => x.OperationId == operationId).OrderByDescending(x => x.StartedOn).ThenByDescending(x => x.SourceNo).ToArray();

    public IReadOnlyList<MomOperator> ListActiveOperators() => operatorResolver.ListActive();

    public void EnsureWorkOrderCanComplete(Guid workOrderId)
    {
        var operations = List(workOrderId);
        if (operations.Count > 0 && operations.Any(x => x.Status != MomOperationStatus.Completed))
            throw new InvalidOperationException("工单存在未完工工序，不能完工。");
    }

    public IReadOnlyList<MomWorkOrderOperation> EnsureOperations(Guid workOrderId)
    {
        var workOrder = FindWorkOrder(workOrderId);
        EnsureOperationWorkOrder(workOrder);
        var existing = List(workOrderId);
        if (existing.Count > 0) { foreach (var operation in existing) EnsureWorkCenter(operation); return existing; }

        var requirements = materialKittingService.EnsureRequirements(workOrderId);
        var versionId = requirements.FirstOrDefault()?.ManufacturingVersionId
            ?? throw new InvalidOperationException("工单没有可用的制造版本用料快照。");
        var sequences = componentRepository.List().Where(x => x.ManufacturingVersionId == versionId)
            .Select(x => x.OperationSequence).Distinct().OrderBy(x => x).ToArray();
        if (sequences.Length == 0) throw new InvalidOperationException("制造版本没有可执行工序。");
        var standards = operationStandardRepository.List().Where(x => x.ManufacturingVersionId == versionId).ToDictionary(x => x.OperationSequence);
        if (standards.Count == 0) EnsureWorkCenter(workOrder);
        var operations = sequences.Select(sequence =>
        {
            var standard = standards.GetValueOrDefault(sequence);
            var workCenterId = standard?.WorkCenterId ?? workOrder.WorkCenterId
                ?? throw new InvalidOperationException("工单未绑定工作中心，不能生成工序。");
            EnsureWorkCenterId(workCenterId);
            return new MomWorkOrderOperation(workOrder.Id, sequence, standard?.OperationCode ?? $"OP-{sequence:000}", standard?.OperationName ?? $"工序 {sequence}",
                workCenterId, workOrder.PlannedQuantity, standardSetupHours: standard?.SetupHours ?? 0, standardRunHoursPerUnit: standard?.RunHoursPerUnit ?? 0);
        }).ToArray();
        void Persist() { foreach (var operation in operations) operationRepository.Add(operation); }
        if (transactions is null) Persist(); else transactions.Execute(Persist);
        return operations;
    }

    public void Accept(Guid operationId, string actor, DateTime? occurredOn = null)
    {
        var operation = FindOperation(operationId); var workOrder = FindWorkOrder(operation.WorkOrderId);
        EnsureActionWorkOrder(workOrder); EnsureWorkCenter(operation);
        EnsurePreviousOperationsCompleted(operation);
        var snapshot = Lifecycle(operation);
        operation.Accept(actor, occurredOn ?? DateTime.Now);
        PersistLifecycle(operation, snapshot);
    }

    public void Start(Guid operationId, DateTime? occurredOn = null)
    {
        var operation = FindOperation(operationId); var workOrder = FindWorkOrder(operation.WorkOrderId);
        EnsureActionWorkOrder(workOrder); EnsureWorkCenter(operation);
        var snapshot = Lifecycle(operation); operation.Start(occurredOn ?? DateTime.Now); PersistLifecycle(operation, snapshot);
    }

    public void Pause(Guid operationId, DateTime? occurredOn = null)
    {
        var operation = FindOperation(operationId); var workOrder = FindWorkOrder(operation.WorkOrderId);
        EnsureActionWorkOrder(workOrder); EnsureWorkCenter(operation);
        var snapshot = Lifecycle(operation); operation.Pause(occurredOn ?? DateTime.Now); PersistLifecycle(operation, snapshot);
    }

    public void Resume(Guid operationId, DateTime? occurredOn = null)
    {
        var operation = FindOperation(operationId); var workOrder = FindWorkOrder(operation.WorkOrderId);
        EnsureActionWorkOrder(workOrder); EnsureWorkCenter(operation);
        var snapshot = Lifecycle(operation); operation.Resume(occurredOn ?? DateTime.Now); PersistLifecycle(operation, snapshot);
    }

    public void Complete(Guid operationId, DateTime? occurredOn = null)
    {
        var operation = FindOperation(operationId); var workOrder = FindWorkOrder(operation.WorkOrderId);
        EnsureActionWorkOrder(workOrder); EnsureWorkCenter(operation);
        qualityInspectionGate?.EnsureOperationCanComplete(operation.Id);
        var snapshot = Lifecycle(operation); operation.Complete(occurredOn ?? DateTime.Now); PersistLifecycle(operation, snapshot);
    }

    public MomWorkOrderOperationReport Report(Guid operationId, decimal goodQuantity, decimal scrapQuantity,
        string actor, DateTime? occurredOn = null, string? notes = null, string? otherInfo = null)
    {
        var operation = FindOperation(operationId); var workOrder = FindWorkOrder(operation.WorkOrderId);
        if (workOrder.Status != MomWorkOrderStatus.InProgress) throw new InvalidOperationException("只有执行中的工单可以登记工序报工。");
        EnsureWorkCenter(operation);
        var originalReported = operation.ReportedQuantity; var originalGood = operation.GoodQuantity; var originalScrap = operation.ScrapQuantity;
        operation.Report(goodQuantity, scrapQuantity);
        var reportId = Guid.CreateVersion7(); var date = occurredOn ?? DateTime.Now;
        var report = new MomWorkOrderOperationReport(operation.Id, workOrder.Id, operation.WorkCenterId, goodQuantity, scrapQuantity,
            MomWorkOrderOperationReport.BuildSourceNo(operation.Id, reportId), date, actor, notes, otherInfo, reportId);
        void Persist() { operationRepository.Update(operation); reportRepository.Add(report); }
        if (transactions is null) Persist();
        else transactions.Execute(Persist, _ => operation.RestoreReportTotals(originalReported, originalGood, originalScrap));
        return report;
    }

    /// <summary>
    /// 对尚未更正的原始报工追加不可变抵减记录，并在同一事务中回写工序累计量。
    /// 执行中的工序可直接更正；已完工工序只有在工单仍执行中且后续工序尚未开始时才允许重开并更正，避免破坏后续工序顺序。
    /// </summary>
    public MomWorkOrderOperationReportCorrection CorrectReport(Guid reportId, decimal goodQuantity, decimal scrapQuantity,
        string actor, DateTime? occurredOn = null, string? notes = null, string? otherInfo = null)
    {
        var report = reportRepository.List().FirstOrDefault(x => x.Id == reportId)
            ?? throw new InvalidOperationException("报工记录不存在。");
        var operation = FindOperation(report.OperationId);
        var workOrder = FindWorkOrder(operation.WorkOrderId);
        if (report.WorkOrderId != workOrder.Id || report.OperationId != operation.Id || report.WorkCenterId != operation.WorkCenterId)
            throw new InvalidOperationException("报工记录与工序归属不一致，不能更正。");
        if (workOrder.Status != MomWorkOrderStatus.InProgress)
            throw new InvalidOperationException("只有执行中的工单可以更正工序报工。");
        var reopened = false;
        var lifecycleSnapshot = Lifecycle(operation);
        if (operation.Status == MomOperationStatus.Completed)
        {
            if (workOrder.Status != MomWorkOrderStatus.InProgress)
                throw new InvalidOperationException("只有执行中的工单可以更正已完工工序报工。");
            EnsureNoLaterOperationStarted(operation);
            operation.ReopenForCorrection();
            reopened = true;
        }
        else if (operation.Status != MomOperationStatus.InProgress)
            throw new InvalidOperationException("只有执行中的工序可以更正报工。");
        EnsureWorkCenter(operation);

        var corrected = correctionRepository.List().Where(x => x.ReportId == report.Id).ToArray();
        var remainingGood = Round(report.GoodQuantity - corrected.Sum(x => x.GoodQuantity));
        var remainingScrap = Round(report.ScrapQuantity - corrected.Sum(x => x.ScrapQuantity));
        var good = Round(goodQuantity); var scrap = Round(scrapQuantity);
        if (good < 0 || scrap < 0) throw new ArgumentOutOfRangeException(nameof(goodQuantity), "报工更正数量不能为负数。");
        if (good > remainingGood) throw new InvalidOperationException("良品更正数量不能超过原报工未更正数量。");
        if (scrap > remainingScrap) throw new InvalidOperationException("不良品更正数量不能超过原报工未更正数量。");

        var correctionId = Guid.CreateVersion7();
        var correction = new MomWorkOrderOperationReportCorrection(report.Id, operation.Id, workOrder.Id, operation.WorkCenterId,
            good, scrap, MomWorkOrderOperationReportCorrection.BuildSourceNo(report.Id, correctionId), occurredOn ?? DateTime.Now,
            actor, notes, otherInfo, correctionId);
        var originalReported = operation.ReportedQuantity;
        var originalGood = operation.GoodQuantity;
        var originalScrap = operation.ScrapQuantity;
        operation.RestoreReportTotals(originalReported - correction.Quantity, originalGood - correction.GoodQuantity, originalScrap - correction.ScrapQuantity);

        void Persist()
        {
            operationRepository.Update(operation);
            correctionRepository.Add(correction);
        }

        if (transactions is null) Persist();
        else transactions.Execute(Persist, _ =>
        {
            operation.RestoreReportTotals(originalReported, originalGood, originalScrap);
            if (reopened) operation.RestoreLifecycle(lifecycleSnapshot.Status, lifecycleSnapshot.AcceptedBy, lifecycleSnapshot.AcceptedOn, lifecycleSnapshot.StartedOn, lifecycleSnapshot.PausedOn, lifecycleSnapshot.CompletedOn);
        });
        return correction;
    }

    /// <summary>
    /// 登记工序实际工时。员工必须来自启用账号且未离职/停职的受控名单，同一员工的时间段不能重叠。
    /// </summary>
    public MomWorkOrderOperationWorkLog LogWork(Guid operationId, Guid operatorUserId, Guid equipmentId, DateTime startedOn, DateTime endedOn,
        string? notes = null, string? otherInfo = null)
    {
        var operation = FindOperation(operationId);
        var workOrder = FindWorkOrder(operation.WorkOrderId);
        if (workOrder.Status != MomWorkOrderStatus.InProgress)
            throw new InvalidOperationException("只有执行中的工单可以登记工序工时。");
        if (operation.Status != MomOperationStatus.InProgress)
            throw new InvalidOperationException("只有执行中的工序可以登记工时。");
        EnsureWorkCenter(operation);
        var operatorInfo = operatorResolver.GetActive(operatorUserId)
            ?? throw new InvalidOperationException("只能选择启用且在职的员工登记工时。");
        var equipmentInfo = equipmentResolver.GetActive(equipmentId)
            ?? throw new InvalidOperationException("只能选择启用设备登记工时。");
        if (equipmentInfo.WorkCenterId != operation.WorkCenterId)
            throw new InvalidOperationException("所选设备不属于当前工序工作中心。");
        if (endedOn <= startedOn) throw new InvalidOperationException("工时结束时间必须晚于开始时间。");

        var overlap = workLogRepository.List().Any(x => x.OperatorUserId == operatorUserId
            && startedOn < x.EndedOn && endedOn > x.StartedOn);
        if (overlap) throw new InvalidOperationException("同一员工的工时区间不能重叠。");

        var logId = Guid.CreateVersion7();
        var workLog = new MomWorkOrderOperationWorkLog(operation.Id, workOrder.Id, operation.WorkCenterId, operatorInfo.UserId,
            operatorInfo.DisplayName, equipmentInfo.Id, equipmentInfo.Name, startedOn, endedOn,
            MomWorkOrderOperationWorkLog.BuildSourceNo(operation.Id, logId), notes, otherInfo, logId);
        void Persist() => workLogRepository.Add(workLog);
        if (transactions is null) Persist(); else transactions.Execute(Persist);
        return workLog;
    }

    private void PersistLifecycle(MomWorkOrderOperation operation, OperationLifecycleSnapshot snapshot)
    {
        void Persist() => operationRepository.Update(operation);
        if (transactions is null) Persist();
        else transactions.Execute(Persist, _ => operation.RestoreLifecycle(snapshot.Status, snapshot.AcceptedBy, snapshot.AcceptedOn, snapshot.StartedOn, snapshot.PausedOn, snapshot.CompletedOn));
    }

    private void EnsurePreviousOperationsCompleted(MomWorkOrderOperation operation)
    {
        var previous = List(operation.WorkOrderId).Where(x => x.OperationSequence < operation.OperationSequence);
        if (previous.Any(x => x.Status != MomOperationStatus.Completed)) throw new InvalidOperationException("前序工序未完工，不能受理当前工序。");
    }

    private void EnsureNoLaterOperationStarted(MomWorkOrderOperation operation)
    {
        var later = List(operation.WorkOrderId).Where(x => x.OperationSequence > operation.OperationSequence);
        if (later.Any(x => x.Status != MomOperationStatus.Pending))
            throw new InvalidOperationException("后续工序已经开始，不能重开当前已完工工序。");
    }

    private void EnsureWorkCenter(MomWorkOrder workOrder)
    {
        if (workOrder.WorkCenterId is not Guid workCenterId) throw new InvalidOperationException("工单未绑定工作中心，不能执行工序。");
        EnsureWorkCenterId(workCenterId);
    }

    private void EnsureWorkCenter(MomWorkOrderOperation operation) => EnsureWorkCenterId(operation.WorkCenterId);

    private void EnsureWorkCenterId(Guid workCenterId)
    {
        var center = workCenterRepository.List().FirstOrDefault(x => x.Id == workCenterId) ?? throw new InvalidOperationException("工作中心不存在。");
        if (center.Status != MomMasterDataStatus.Active) throw new InvalidOperationException("工作中心已停用，不能执行工序。");
    }

    private static void EnsureOperationWorkOrder(MomWorkOrder workOrder)
    {
        if (workOrder.Status is not (MomWorkOrderStatus.Released or MomWorkOrderStatus.InProgress or MomWorkOrderStatus.Completed))
            throw new InvalidOperationException("只有已下达、执行中或已完工工单可以生成工序。");
    }

    private static void EnsureActionWorkOrder(MomWorkOrder workOrder)
    {
        if (workOrder.Status is not (MomWorkOrderStatus.Released or MomWorkOrderStatus.InProgress))
            throw new InvalidOperationException("只有已下达或执行中的工单可以执行工序动作。");
    }

    private MomWorkOrder FindWorkOrder(Guid id) => workOrderRepository.List().FirstOrDefault(x => x.Id == id)
        ?? throw new InvalidOperationException("制造工单不存在。");
    private MomWorkOrderOperation FindOperation(Guid id) => operationRepository.List().FirstOrDefault(x => x.Id == id)
        ?? throw new InvalidOperationException("工序不存在。");

    private static OperationLifecycleSnapshot Lifecycle(MomWorkOrderOperation item) => new(item.Status, item.AcceptedBy, item.AcceptedOn, item.StartedOn, item.PausedOn, item.CompletedOn);
    private static decimal Round(decimal value) => decimal.Round(value, 6, MidpointRounding.AwayFromZero);
    private sealed record OperationLifecycleSnapshot(MomOperationStatus Status, string? AcceptedBy, DateTime? AcceptedOn, DateTime? StartedOn, DateTime? PausedOn, DateTime? CompletedOn);
}
