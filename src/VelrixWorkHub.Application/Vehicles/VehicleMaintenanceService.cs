using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Vehicles;

public interface IOaVehicleMaintenanceRepository
{
    IReadOnlyList<OaVehicleMaintenance> List(Guid? vehicleId = null);
    OaVehicleMaintenance? Get(Guid id);
    void Add(OaVehicleMaintenance item);
    void Update(OaVehicleMaintenance item);
}

public sealed class VehicleMaintenanceService(
    IOaVehicleMaintenanceRepository repository,
    VehicleService vehicleService,
    IWorkflowTransactionBoundary? transactions = null)
{
    public IReadOnlyList<OaVehicleMaintenance> ListByVehicle(Guid vehicleId)
        => vehicleId == Guid.Empty ? [] : repository.List(vehicleId).OrderByDescending(x => x.StartedAt).ToArray();

    public OaVehicleMaintenance Start(Guid vehicleId, Guid reporterUserId, string reporterName, DateTime startedAt, decimal? mileage,
        string description, string? serviceProvider, decimal? cost, string? otherInfo)
    {
        var vehicle = vehicleService.GetVehicle(vehicleId) ?? throw new InvalidOperationException("车辆不存在或已被删除。");
        if (vehicle.Status != OaVehicleStatus.Available) throw new InvalidOperationException("只有可用车辆才能开始维修。 ");
        if (repository.List(vehicleId).Any(x => x.Status == OaVehicleMaintenanceStatus.Open)) throw new InvalidOperationException("车辆已有进行中的维修记录。 ");
        var item = new OaVehicleMaintenance(vehicleId, reporterUserId, reporterName, startedAt, mileage, description, serviceProvider, cost, otherInfo, DateTime.Now);
        var previousVehicleStatus = vehicle.Status;
        void Core()
        {
            repository.Add(item);
            vehicleService.SetVehicleStatus(vehicle, OaVehicleStatus.Maintenance);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => vehicle.SetStatus(previousVehicleStatus));
        return item;
    }

    public void Edit(OaVehicleMaintenance item, Guid actorUserId, string reporterName, DateTime startedAt, decimal? mileage,
        string description, string? serviceProvider, decimal? cost, string? otherInfo)
    {
        EnsureReporter(item, actorUserId);
        item.Edit(reporterName, startedAt, mileage, description, serviceProvider, cost, otherInfo);
        repository.Update(item);
    }

    public void Complete(OaVehicleMaintenance item, Guid actorUserId, string notes)
    {
        EnsureReporter(item, actorUserId);
        var vehicle = vehicleService.GetVehicle(item.VehicleId) ?? throw new InvalidOperationException("车辆不存在或已被删除。");
        if (vehicle.Status != OaVehicleStatus.Maintenance) throw new InvalidOperationException("车辆当前不处于维修中，不能完成维修。 ");
        var previousStatus = item.Status;
        var previousNotes = item.CompletionNotes;
        var previousCompletedAt = item.CompletedAt;
        var previousVehicleStatus = vehicle.Status;
        void Core()
        {
            item.Complete(notes, DateTime.Now);
            vehicleService.SetVehicleStatus(vehicle, OaVehicleStatus.Available);
            repository.Update(item);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => { item.SetStatus(previousStatus); item.SetCompletionData(previousNotes, previousCompletedAt); vehicle.SetStatus(previousVehicleStatus); });
    }

    public void Cancel(OaVehicleMaintenance item, Guid actorUserId, string? notes)
    {
        EnsureReporter(item, actorUserId);
        var vehicle = vehicleService.GetVehicle(item.VehicleId) ?? throw new InvalidOperationException("车辆不存在或已被删除。");
        if (vehicle.Status != OaVehicleStatus.Maintenance) throw new InvalidOperationException("车辆当前不处于维修中，不能取消维修。 ");
        var previousStatus = item.Status;
        var previousNotes = item.CompletionNotes;
        var previousCompletedAt = item.CompletedAt;
        var previousVehicleStatus = vehicle.Status;
        void Core()
        {
            item.Cancel(notes);
            vehicleService.SetVehicleStatus(vehicle, OaVehicleStatus.Available);
            repository.Update(item);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => { item.SetStatus(previousStatus); item.SetCompletionData(previousNotes, previousCompletedAt); vehicle.SetStatus(previousVehicleStatus); });
    }

    private static void EnsureReporter(OaVehicleMaintenance item, Guid actorUserId)
    {
        if (actorUserId == Guid.Empty || item.ReporterUserId != actorUserId) throw new UnauthorizedAccessException("当前用户不能操作其他员工登记的车辆维修记录。 ");
    }
}
