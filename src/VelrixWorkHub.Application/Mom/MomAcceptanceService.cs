using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Application.SalesOrders;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

/// <summary>
/// MOM-08E FAT/SAT 验收应用服务。验收单只引用销售订单、发运和 PMS 项目，不直接跨模块写表。
/// </summary>
public sealed class MomAcceptanceService(
    IMomAcceptanceRepository repository,
    IMomAcceptanceChecklistRepository checklistRepository,
    ISalesOrderRepository salesOrderRepository,
    IMomFinishedGoodsShipmentRepository shipmentRepository,
    IPmsProjectRepository projectRepository,
    IWorkflowTransactionBoundary? transactions = null)
{
    public IReadOnlyList<MomAcceptance> List(Guid? salesOrderId = null, MomAcceptanceType? type = null, MomAcceptanceStatus? status = null)
    {
        var query = repository.List().AsEnumerable();
        if (salesOrderId is Guid selectedOrder) query = query.Where(x => x.SalesOrderId == selectedOrder);
        if (type is MomAcceptanceType selectedType) query = query.Where(x => x.AcceptanceType == selectedType);
        if (status is MomAcceptanceStatus selectedStatus) query = query.Where(x => x.Status == selectedStatus);
        return query.OrderByDescending(x => x.PlannedDate).ThenByDescending(x => x.AcceptanceNo).ToArray();
    }

    public IReadOnlyList<MomAcceptanceChecklistItem> ListItems(Guid acceptanceId)
        => checklistRepository.List(acceptanceId).OrderBy(x => x.LineNo).ToArray();

    public MomAcceptance Create(MomAcceptanceType type, Guid salesOrderId, Guid? shipmentId, Guid? pmsProjectId,
        DateOnly plannedDate, string actor, string? serialNo = null, string? locationOrMode = null,
        string? participants = null, string? notes = null, string? otherInfo = null)
    {
        var order = FindOrder(salesOrderId);
        if (order.Status is not (SalesOrderStatus.Submitted or SalesOrderStatus.Shipped))
            throw new InvalidOperationException("只有已提交或已发运的销售订单可以建立 FAT/SAT 验收单。");

        var shipment = ResolveShipment(order, shipmentId);
        if (type == MomAcceptanceType.Sat && (shipment is null || order.Status != SalesOrderStatus.Shipped))
            throw new InvalidOperationException("SAT 验收必须绑定已发运销售订单和发运记录。");
        if (pmsProjectId is Guid projectId)
        {
            if (projectRepository.List().All(x => x.Id != projectId)) throw new InvalidOperationException("PMS 项目不存在。");
            if (order.PmsProjectId != projectId) throw new InvalidOperationException("PMS 项目必须与销售订单来源一致。");
        }
        if (repository.List().Any(x => x.SalesOrderId == order.Id && x.AcceptanceType == type && x.Status is not (MomAcceptanceStatus.Cancelled or MomAcceptanceStatus.Failed)
            && string.Equals(x.SerialNo, string.IsNullOrWhiteSpace(serialNo) ? null : serialNo.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("同一销售订单和序列号不能重复建立有效的同类型验收单。");

        var item = new MomAcceptance(type, order.Id, shipment?.Id, pmsProjectId, order.CustomerId, order.ProductId,
            plannedDate, actor, serialNo, locationOrMode, participants, notes, otherInfo);
        Persist(() => repository.Add(item));
        return item;
    }

    public MomAcceptanceChecklistItem AddItem(Guid acceptanceId, int lineNo, string itemCode, string itemName, string requirement, string? otherInfo = null)
    {
        var acceptance = Find(acceptanceId);
        EnsureDraft(acceptance);
        if (checklistRepository.List(acceptanceId).Any(x => x.LineNo == lineNo)) throw new InvalidOperationException("同一验收单的检查项行号已存在。");
        var item = new MomAcceptanceChecklistItem(acceptanceId, lineNo, itemCode, itemName, requirement, otherInfo);
        Persist(() => checklistRepository.Add(item));
        return item;
    }

    public void SetItemResult(Guid acceptanceId, Guid itemId, MomAcceptanceItemResult result, string? remark, string actor, DateTime? checkedOn = null)
    {
        var acceptance = Find(acceptanceId); EnsureDraft(acceptance);
        var item = FindItem(acceptanceId, itemId); var original = (item.Result, item.Remark, item.CheckedBy, item.CheckedOn);
        item.SetResult(result, remark, actor, checkedOn ?? DateTime.Now);
        Persist(() => checklistRepository.Update(item), _ => item.RestoreResult(original.Result, original.Remark, original.CheckedBy, original.CheckedOn));
    }

    public void RemoveItem(Guid acceptanceId, Guid itemId)
    {
        var acceptance = Find(acceptanceId); EnsureDraft(acceptance);
        _ = FindItem(acceptanceId, itemId);
        Persist(() => checklistRepository.Remove(itemId));
    }

    public void Submit(Guid acceptanceId, string actor, DateTime? submittedOn = null)
    {
        var acceptance = Find(acceptanceId); EnsureDraft(acceptance);
        var items = ListItems(acceptanceId);
        if (items.Count == 0) throw new InvalidOperationException("验收单至少需要一个检查项。");
        if (items.Any(x => x.Result == MomAcceptanceItemResult.Pending)) throw new InvalidOperationException("所有检查项必须先登记结果才能提交验收单。");
        var original = Snapshot(acceptance); acceptance.Submit(actor, submittedOn ?? DateTime.Now);
        Persist(() => repository.Update(acceptance), _ => Restore(acceptance, original));
    }

    public void Complete(Guid acceptanceId, MomAcceptanceStatus result, string actor, string conclusion, string? failureReason, DateTime? completedOn = null)
    {
        var acceptance = Find(acceptanceId);
        if (acceptance.Status != MomAcceptanceStatus.Submitted) throw new InvalidOperationException("只有已提交验收单可以完成验收。");
        var items = ListItems(acceptanceId);
        if (items.Count == 0 || items.Any(x => x.Result == MomAcceptanceItemResult.Pending)) throw new InvalidOperationException("验收单检查项尚未全部完成。");
        if (result == MomAcceptanceStatus.Passed && items.Any(x => x.Result == MomAcceptanceItemResult.Failed)) throw new InvalidOperationException("存在不通过检查项，不能将验收单判定为通过。");
        if (result == MomAcceptanceStatus.Failed && items.All(x => x.Result != MomAcceptanceItemResult.Failed)) throw new InvalidOperationException("验收失败必须至少有一个不通过检查项。");
        var original = Snapshot(acceptance); acceptance.Complete(result, actor, completedOn ?? DateTime.Now, conclusion, failureReason);
        Persist(() => repository.Update(acceptance), _ => Restore(acceptance, original));
    }

    public void Cancel(Guid acceptanceId, string actor, string reason, DateTime? cancelledOn = null)
    {
        var acceptance = Find(acceptanceId); var original = Snapshot(acceptance); acceptance.Cancel(actor, cancelledOn ?? DateTime.Now, reason);
        Persist(() => repository.Update(acceptance), _ => Restore(acceptance, original));
    }

    private SalesOrder FindOrder(Guid id) => salesOrderRepository.List().FirstOrDefault(x => x.Id == id)
        ?? throw new InvalidOperationException("销售订单不存在。");

    private MomFinishedGoodsShipment? ResolveShipment(SalesOrder order, Guid? shipmentId)
    {
        if (shipmentId is not Guid selected) return null;
        var shipment = shipmentRepository.List().FirstOrDefault(x => x.Id == selected)
            ?? throw new InvalidOperationException("发运记录不存在。");
        if (shipment.SalesOrderId != order.Id) throw new InvalidOperationException("发运记录必须属于当前销售订单。");
        return shipment;
    }

    private MomAcceptance Find(Guid id) => repository.List().FirstOrDefault(x => x.Id == id)
        ?? throw new InvalidOperationException("验收单不存在。");

    private MomAcceptanceChecklistItem FindItem(Guid acceptanceId, Guid id) => checklistRepository.List(acceptanceId).FirstOrDefault(x => x.Id == id)
        ?? throw new InvalidOperationException("验收检查项不存在。");

    private static void EnsureDraft(MomAcceptance acceptance)
    {
        if (acceptance.Status != MomAcceptanceStatus.Draft) throw new InvalidOperationException("只有草稿验收单可以维护检查项。");
    }

    private void Persist(Action operation, Action<Exception>? rollback = null)
    {
        if (transactions is null) operation();
        else transactions.Execute(operation, rollback);
    }

    private static AcceptanceSnapshot Snapshot(MomAcceptance x) => new(x.Status, x.SubmittedBy, x.SubmittedOn, x.CompletedBy, x.CompletedOn,
        x.Conclusion, x.FailureReason, x.CancelledBy, x.CancelledOn, x.CancellationReason);

    private static void Restore(MomAcceptance x, AcceptanceSnapshot s) => x.RestoreState(s.Status, s.SubmittedBy, s.SubmittedOn, s.CompletedBy, s.CompletedOn,
        s.Conclusion, s.FailureReason, s.CancelledBy, s.CancelledOn, s.CancellationReason);

    private sealed record AcceptanceSnapshot(MomAcceptanceStatus Status, string? SubmittedBy, DateTime? SubmittedOn, string? CompletedBy,
        DateTime? CompletedOn, string? Conclusion, string? FailureReason, string? CancelledBy, DateTime? CancelledOn, string? CancellationReason);
}
