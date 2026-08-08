using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

/// <summary>
/// MOM-07C 不合格处置与返工关联。只建立可追溯的业务处置账，不伪造库存扣减或报工结果。
/// </summary>
public sealed class MomQualityNonconformanceService(
    IMomQualityNonconformanceRepository nonconformanceRepository,
    IMomQualityDispositionRepository dispositionRepository,
    IMomQualityInspectionRepository inspectionRepository,
    IMomWorkOrderRepository workOrderRepository,
    IMomWorkOrderOperationRepository operationRepository,
    IWorkflowTransactionBoundary? transactions = null)
{
    public IReadOnlyList<MomQualityNonconformance> List(Guid? workOrderId = null, MomQualityNonconformanceStatus? status = null)
    {
        var query = nonconformanceRepository.List().AsEnumerable();
        if (workOrderId is Guid selected) query = query.Where(x => x.WorkOrderId == selected);
        if (status is MomQualityNonconformanceStatus selectedStatus) query = query.Where(x => x.Status == selectedStatus);
        return query.OrderByDescending(x => x.CreatedOn).ThenByDescending(x => x.NonconformanceNo).ToArray();
    }

    public IReadOnlyList<MomQualityDisposition> ListDispositions(Guid? nonconformanceId = null)
    {
        var query = dispositionRepository.List().AsEnumerable();
        if (nonconformanceId is Guid selected) query = query.Where(x => x.NonconformanceId == selected);
        return query.OrderByDescending(x => x.CreatedOn).ThenByDescending(x => x.SourceNo).ToArray();
    }

    public MomQualityNonconformance CreateFromFailedInspection(Guid inspectionId, string defectCode, string description,
        MomQualityNonconformanceSeverity severity, DateTime? createdOn = null, string? otherInfo = null)
    {
        var inspection = FindInspection(inspectionId);
        if (inspection.Status != MomQualityInspectionStatus.Failed) throw new InvalidOperationException("只有不通过的质量检验可以登记不合格。");
        if (nonconformanceRepository.List().Any(x => x.InspectionId == inspectionId))
            throw new InvalidOperationException("同一质量检验只能登记一条不合格记录。");
        var item = new MomQualityNonconformance(inspection.Id, inspection.WorkOrderId, inspection.OperationId, inspection.ProductId,
            inspection.BatchNo, defectCode, description, inspection.RejectedQuantity, severity, createdOn ?? DateTime.Now, otherInfo);
        void Persist() => nonconformanceRepository.Add(item);
        if (transactions is null) Persist(); else transactions.Execute(Persist);
        return item;
    }

    public MomQualityDisposition CreateDisposition(Guid nonconformanceId, MomQualityDispositionAction action,
        Guid? targetWorkOrderId = null, Guid? targetOperationId = null, DateTime? createdOn = null, string? notes = null, string? otherInfo = null)
    {
        var nonconformance = FindNonconformance(nonconformanceId);
        if (nonconformance.Status != MomQualityNonconformanceStatus.Open) throw new InvalidOperationException("只有未处置的不合格记录可以建立处置方案。");
        EnsureReworkTarget(nonconformance, action, targetWorkOrderId, targetOperationId);
        var disposition = new MomQualityDisposition(nonconformance.Id, action, nonconformance.Quantity, targetWorkOrderId, targetOperationId,
            createdOn ?? DateTime.Now, notes, otherInfo);
        var originalStatus = nonconformance.Status; var originalDispositionId = nonconformance.DispositionId;
        nonconformance.AssignDisposition(disposition.Id);
        void Persist() { nonconformanceRepository.Update(nonconformance); dispositionRepository.Add(disposition); }
        if (transactions is null) Persist();
        else transactions.Execute(Persist, _ => nonconformance.RestoreState(originalStatus, originalDispositionId, null, null, null));
        return disposition;
    }

    public void CompleteDisposition(Guid dispositionId, string actor, DateTime? completedOn = null, string? notes = null)
    {
        var disposition = FindDisposition(dispositionId); var nonconformance = FindNonconformance(disposition.NonconformanceId);
        if (nonconformance.DispositionId != disposition.Id) throw new InvalidOperationException("不合格处置与主记录关联不一致。");
        var originalDispositionStatus = disposition.Status; var originalCompletedOn = disposition.CompletedOn; var originalCompletedBy = disposition.CompletedBy;
        var originalNonconformanceStatus = nonconformance.Status; var originalNonconformanceDispositionId = nonconformance.DispositionId;
        disposition.Complete(actor, completedOn ?? DateTime.Now, notes); nonconformance.Close(actor, completedOn ?? DateTime.Now, notes);
        void Persist() { dispositionRepository.Update(disposition); nonconformanceRepository.Update(nonconformance); }
        if (transactions is null) Persist();
        else transactions.Execute(Persist, _ => { disposition.RestoreState(originalDispositionStatus, originalCompletedOn, originalCompletedBy); nonconformance.RestoreState(originalNonconformanceStatus, originalNonconformanceDispositionId, null, null, null); });
    }

    public void CancelDisposition(Guid dispositionId)
    {
        var disposition = FindDisposition(dispositionId); var nonconformance = FindNonconformance(disposition.NonconformanceId);
        if (nonconformance.DispositionId != disposition.Id) throw new InvalidOperationException("不合格处置与主记录关联不一致。");
        var originalStatus = nonconformance.Status; var originalDispositionId = nonconformance.DispositionId;
        disposition.Cancel(); nonconformance.RestoreState(MomQualityNonconformanceStatus.Open, null, null, null, null);
        void Persist() { dispositionRepository.Update(disposition); nonconformanceRepository.Update(nonconformance); }
        if (transactions is null) Persist();
        else transactions.Execute(Persist, _ => nonconformance.RestoreState(originalStatus, originalDispositionId, null, null, null));
    }

    private void EnsureReworkTarget(MomQualityNonconformance nonconformance, MomQualityDispositionAction action, Guid? targetWorkOrderId, Guid? targetOperationId)
    {
        if (action != MomQualityDispositionAction.Rework) return;
        var targetWorkOrder = workOrderRepository.List().FirstOrDefault(x => x.Id == targetWorkOrderId)
            ?? throw new InvalidOperationException("返工目标工单不存在。");
        if (targetWorkOrder.Status is MomWorkOrderStatus.Cancelled or MomWorkOrderStatus.Completed)
            throw new InvalidOperationException("已取消或已完工工单不能作为返工目标。");
        var sourceWorkOrder = workOrderRepository.List().FirstOrDefault(x => x.Id == nonconformance.WorkOrderId)
            ?? throw new InvalidOperationException("不合格来源工单不存在。");
        if (targetWorkOrder.ProductId != sourceWorkOrder.ProductId) throw new InvalidOperationException("返工目标工单商品必须与不合格来源一致。");
        var operation = operationRepository.List().FirstOrDefault(x => x.Id == targetOperationId)
            ?? throw new InvalidOperationException("返工目标工序不存在。");
        if (operation.WorkOrderId != targetWorkOrder.Id) throw new InvalidOperationException("返工目标工序不属于目标工单。");
        if (operation.Status == MomOperationStatus.Cancelled) throw new InvalidOperationException("已取消工序不能作为返工目标。");
    }

    private MomQualityInspection FindInspection(Guid id) => inspectionRepository.List().FirstOrDefault(x => x.Id == id)
        ?? throw new InvalidOperationException("质量检验记录不存在。");
    private MomQualityNonconformance FindNonconformance(Guid id) => nonconformanceRepository.List().FirstOrDefault(x => x.Id == id)
        ?? throw new InvalidOperationException("不合格记录不存在。");
    private MomQualityDisposition FindDisposition(Guid id) => dispositionRepository.List().FirstOrDefault(x => x.Id == id)
        ?? throw new InvalidOperationException("不合格处置不存在。");
}
