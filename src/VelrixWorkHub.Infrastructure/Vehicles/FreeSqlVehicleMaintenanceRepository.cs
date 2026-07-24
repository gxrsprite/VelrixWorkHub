using FreeSql;
using VelrixWorkHub.Application.Vehicles;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Vehicles;

public sealed class FreeSqlVehicleMaintenanceRepository(IFreeSql fsql) : IOaVehicleMaintenanceRepository
{
    public IReadOnlyList<OaVehicleMaintenance> List(Guid? vehicleId = null)
    {
        var query = fsql.Select<OaVehicleMaintenanceRecord>();
        if (vehicleId is Guid id) query = query.Where(x => x.VehicleId == id);
        return query.OrderByDescending(x => x.StartedAt).ToList().Select(ToDomain).ToArray();
    }

    public OaVehicleMaintenance? Get(Guid id) => fsql.Select<OaVehicleMaintenanceRecord>().Where(x => x.Id == id).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(OaVehicleMaintenance item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    public void Update(OaVehicleMaintenance item)
    {
        var rows = fsql.Update<OaVehicleMaintenanceRecord>().SetSource(ToRecord(item)).Where(x => x.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("车辆维修记录不存在或已被删除。 ");
    }

    private static OaVehicleMaintenance ToDomain(OaVehicleMaintenanceRecord x)
    {
        var item = new OaVehicleMaintenance(x.VehicleId, x.ReporterUserId, x.ReporterName, x.StartedAt, x.Mileage, x.Description,
            x.ServiceProvider, x.Cost, x.OtherInfo, x.CreatedAt) { Id = x.Id };
        if (x.Status == OaVehicleMaintenanceStatus.Completed) item.Complete(x.CompletionNotes ?? "已完成", x.CompletedAt ?? x.CreatedAt);
        else if (x.Status == OaVehicleMaintenanceStatus.Cancelled) item.Cancel(x.CompletionNotes);
        return item;
    }

    private static OaVehicleMaintenanceRecord ToRecord(OaVehicleMaintenance x) => new()
    {
        Id = x.Id, VehicleId = x.VehicleId, ReporterUserId = x.ReporterUserId, ReporterName = x.ReporterName, StartedAt = x.StartedAt,
        Mileage = x.Mileage, Description = x.Description, ServiceProvider = x.ServiceProvider, Cost = x.Cost, OtherInfo = x.OtherInfo,
        Status = x.Status, CompletionNotes = x.CompletionNotes, CreatedAt = x.CreatedAt, CompletedAt = x.CompletedAt
    };
}
