using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Application.SalesOrders;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

/// <summary>
/// MOM-08F: turn a shipped product into a traceable customer-owned service asset.
/// The archive never writes LMS data; it only keeps stable links to the shipment, order and optional PMS project.
/// </summary>
public sealed class MomServiceEquipmentService(
    IMomServiceEquipmentRepository repository,
    IMomServiceEquipmentLifecycleRepository lifecycleRepository,
    IMomFinishedGoodsShipmentRepository shipmentRepository,
    IMomFinishedGoodsShipmentAllocationRepository allocationRepository,
    ISalesOrderRepository salesOrderRepository,
    IPmsProjectRepository projectRepository,
    IWorkflowTransactionBoundary? transactions = null)
{
    public IReadOnlyList<MomServiceEquipment> List(Guid? customerId = null, MomServiceEquipmentStatus? status = null)
    {
        var query = repository.List().AsEnumerable();
        if (customerId is Guid selectedCustomerId) query = query.Where(x => x.CustomerId == selectedCustomerId);
        if (status is MomServiceEquipmentStatus selectedStatus) query = query.Where(x => x.Status == selectedStatus);
        return query.OrderBy(x => x.Status).ThenByDescending(x => x.CreatedOn).ThenBy(x => x.EquipmentNo).ToArray();
    }

    public IReadOnlyList<MomServiceEquipmentLifecycleEntry> ListLifecycle(Guid equipmentId)
        => lifecycleRepository.List(equipmentId).OrderByDescending(x => x.OccurredOn).ToArray();

    public MomServiceEquipment CreateFromShipment(Guid shipmentId, string serialNo, string actor, string? equipmentNo = null,
        string? sourceNo = null, Guid? pmsProjectId = null, string? model = null,
        DateOnly? warrantyStartDate = null, DateOnly? warrantyEndDate = null, string? notes = null, string? otherInfo = null)
    {
        var shipment = shipmentRepository.List().FirstOrDefault(x => x.Id == shipmentId)
            ?? throw new InvalidOperationException("发运记录不存在。");
        var order = salesOrderRepository.List().FirstOrDefault(x => x.Id == shipment.SalesOrderId)
            ?? throw new InvalidOperationException("销售订单不存在。");
        if (order.Status != SalesOrderStatus.Shipped) throw new InvalidOperationException("只有已发运销售订单可以建立售后设备档案。");
        if (order.ProductId != shipment.ProductId) throw new InvalidOperationException("发运商品与销售订单商品不一致。");
        if (pmsProjectId is Guid projectId)
        {
            _ = projectRepository.List().FirstOrDefault(x => x.Id == projectId)
                ?? throw new InvalidOperationException("关联 PMS 项目不存在。");
            if (order.PmsProjectId != projectId) throw new InvalidOperationException("PMS 项目必须与销售订单的项目一致。");
        }

        var normalizedSerial = serialNo?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedSerial)) throw new InvalidOperationException("设备序列号不能为空。");
        var selectedSourceNo = ResolveSourceNo(shipment, normalizedSerial, sourceNo);
        if (repository.List().Any(x => x.SerialNo.Equals(normalizedSerial, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("设备序列号已登记售后设备档案。");
        var normalizedEquipmentNo = string.IsNullOrWhiteSpace(equipmentNo) ? NextEquipmentNo() : equipmentNo.Trim();
        if (repository.List().Any(x => x.EquipmentNo.Equals(normalizedEquipmentNo, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("设备档案编号已存在。");

        var item = new MomServiceEquipment(normalizedEquipmentNo, order.CustomerId, order.ProductId, order.Id, shipment.Id,
            normalizedSerial, actor, shipment.ShipmentDate.ToDateTime(TimeOnly.MinValue), selectedSourceNo,
            pmsProjectId, model, warrantyStartDate, warrantyEndDate, notes, otherInfo);
        var lifecycle = new MomServiceEquipmentLifecycleEntry(item.Id, MomServiceEquipmentLifecycleAction.Created, null,
            MomServiceEquipmentStatus.PendingInstallation, actor, item.CreatedOn);
        Persist(() => { repository.Add(item); lifecycleRepository.Add(lifecycle); });
        return item;
    }

    public void Install(Guid equipmentId, string actor, DateOnly installedOn, string location)
    {
        var item = Find(equipmentId);
        var snapshot = Snapshot(item);
        item.Install(actor, installedOn, location);
        var lifecycle = new MomServiceEquipmentLifecycleEntry(item.Id, MomServiceEquipmentLifecycleAction.Installed,
            snapshot.Status, item.Status, actor, installedOn.ToDateTime(TimeOnly.MinValue), location);
        Persist(() => { repository.Update(item); lifecycleRepository.Add(lifecycle); }, _ => Restore(item, snapshot));
    }

    public void Retire(Guid equipmentId, string actor, string reason, DateTime? retiredOn = null)
    {
        var item = Find(equipmentId);
        var snapshot = Snapshot(item);
        item.Retire(actor, retiredOn, reason);
        var lifecycle = new MomServiceEquipmentLifecycleEntry(item.Id, MomServiceEquipmentLifecycleAction.Retired,
            snapshot.Status, item.Status, actor, item.RetiredOn, reason);
        Persist(() => { repository.Update(item); lifecycleRepository.Add(lifecycle); }, _ => Restore(item, snapshot));
    }

    private string? ResolveSourceNo(MomFinishedGoodsShipment shipment, string serialNo, string? sourceNo)
    {
        var allocations = allocationRepository.List(shipment.Id);
        if (allocations.Count == 0)
        {
            if (shipment.SerialNo is not null && !shipment.SerialNo.Equals(serialNo, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("设备序列号与发运记录序列号不一致。");
            return shipment.SourceNo;
        }

        MomFinishedGoodsShipmentAllocation? selected = null;
        if (!string.IsNullOrWhiteSpace(sourceNo))
            selected = allocations.FirstOrDefault(x => x.SourceNo.Equals(sourceNo.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("发运来源分配不存在。");
        else
            selected = allocations.FirstOrDefault(x => x.SerialNo is not null && x.SerialNo.Equals(serialNo, StringComparison.OrdinalIgnoreCase));
        if (selected is null && allocations.Count == 1) selected = allocations[0];
        if (selected is null) throw new InvalidOperationException("多来源发运必须选择对应的序列号来源。");
        if (selected.SerialNo is not null && !selected.SerialNo.Equals(serialNo, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("设备序列号与发运来源序列号不一致。");
        return selected.SourceNo;
    }

    private MomServiceEquipment Find(Guid id) => repository.List().FirstOrDefault(x => x.Id == id)
        ?? throw new InvalidOperationException("售后设备档案不存在。");

    private string NextEquipmentNo()
    {
        string candidate;
        do { candidate = $"MOM-EQ-{Guid.CreateVersion7():N}"; }
        while (repository.List().Any(x => x.EquipmentNo.Equals(candidate, StringComparison.OrdinalIgnoreCase)));
        return candidate;
    }

    private void Persist(Action operation, Action<Exception>? rollback = null)
    {
        if (transactions is null) operation();
        else transactions.Execute(operation, rollback);
    }

    private static EquipmentSnapshot Snapshot(MomServiceEquipment item) => new(item.Status, item.InstallationLocation, item.InstalledBy,
        item.InstalledOn, item.RetiredBy, item.RetiredOn, item.RetiredReason);

    private static void Restore(MomServiceEquipment item, EquipmentSnapshot snapshot) => item.RestoreLifecycle(snapshot.Status,
        snapshot.InstallationLocation, snapshot.InstalledBy, snapshot.InstalledOn, snapshot.RetiredBy, snapshot.RetiredOn, snapshot.RetiredReason);

    private sealed record EquipmentSnapshot(MomServiceEquipmentStatus Status, string? InstallationLocation, string? InstalledBy,
        DateOnly? InstalledOn, string? RetiredBy, DateTime? RetiredOn, string? RetiredReason);
}
