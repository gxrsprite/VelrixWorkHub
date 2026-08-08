using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomServiceEquipmentRepository(IFreeSql fsql) : IMomServiceEquipmentRepository
{
    public IReadOnlyList<MomServiceEquipment> List() => fsql.Select<MomServiceEquipmentRecord>().OrderBy(x => x.Status).OrderByDescending(x => x.CreatedOn).ToList().Select(ToDomain).ToArray();
    public void Add(MomServiceEquipment item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(MomServiceEquipment item)
    {
        var rows = fsql.Update<MomServiceEquipmentRecord>().SetSource(ToRecord(item)).Where(x => x.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("售后设备档案不存在或已被删除。");
    }

    private static MomServiceEquipment ToDomain(MomServiceEquipmentRecord x) => MomServiceEquipment.Restore(x.Id, x.EquipmentNo, x.CustomerId, x.ProductId,
        x.SalesOrderId, x.ShipmentId, x.ShipmentSourceNo, x.PmsProjectId, x.SerialNo, x.Model,
        x.InstallationLocation, x.InstalledBy, x.InstalledOn is DateTime installedOn ? DateOnly.FromDateTime(installedOn) : null,
        x.WarrantyStartDate is DateTime warrantyStart ? DateOnly.FromDateTime(warrantyStart) : null,
        x.WarrantyEndDate is DateTime warrantyEnd ? DateOnly.FromDateTime(warrantyEnd) : null, x.Status, x.CreatedBy, x.CreatedOn,
        x.RetiredBy, x.RetiredOn, x.RetiredReason, x.Notes, x.OtherInfo);

    private static MomServiceEquipmentRecord ToRecord(MomServiceEquipment x) => new()
    {
        Id = x.Id, EquipmentNo = x.EquipmentNo, CustomerId = x.CustomerId, ProductId = x.ProductId, SalesOrderId = x.SalesOrderId,
        ShipmentId = x.ShipmentId, ShipmentSourceNo = x.ShipmentSourceNo, PmsProjectId = x.PmsProjectId, SerialNo = x.SerialNo,
        Model = x.Model, InstallationLocation = x.InstallationLocation, InstalledBy = x.InstalledBy,
        InstalledOn = x.InstalledOn?.ToDateTime(TimeOnly.MinValue), WarrantyStartDate = x.WarrantyStartDate?.ToDateTime(TimeOnly.MinValue),
        WarrantyEndDate = x.WarrantyEndDate?.ToDateTime(TimeOnly.MinValue), Status = x.Status, CreatedBy = x.CreatedBy, CreatedOn = x.CreatedOn,
        RetiredBy = x.RetiredBy, RetiredOn = x.RetiredOn, RetiredReason = x.RetiredReason, Notes = x.Notes, OtherInfo = x.OtherInfo
    };
}

public sealed class FreeSqlMomServiceEquipmentLifecycleRepository(IFreeSql fsql) : IMomServiceEquipmentLifecycleRepository
{
    public IReadOnlyList<MomServiceEquipmentLifecycleEntry> List(Guid? equipmentId = null)
    {
        var query = fsql.Select<MomServiceEquipmentLifecycleEntryRecord>();
        if (equipmentId is Guid selected) query = query.Where(x => x.EquipmentId == selected);
        return query.OrderByDescending(x => x.OccurredOn).ToList().Select(ToDomain).ToArray();
    }

    public void Add(MomServiceEquipmentLifecycleEntry item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    private static MomServiceEquipmentLifecycleEntry ToDomain(MomServiceEquipmentLifecycleEntryRecord x)
        => MomServiceEquipmentLifecycleEntry.Restore(x.Id, x.EquipmentId, x.Action, x.FromStatus, x.ToStatus, x.Actor, x.OccurredOn, x.Reason, x.OtherInfo);

    private static MomServiceEquipmentLifecycleEntryRecord ToRecord(MomServiceEquipmentLifecycleEntry x) => new()
    {
        Id = x.Id, EquipmentId = x.EquipmentId, Action = x.Action, FromStatus = x.FromStatus, ToStatus = x.ToStatus,
        Actor = x.Actor, OccurredOn = x.OccurredOn, Reason = x.Reason, OtherInfo = x.OtherInfo
    };
}
