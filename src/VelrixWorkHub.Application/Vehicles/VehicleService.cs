using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Vehicles;

public interface IOaVehicleRepository
{
    IReadOnlyList<OaVehicle> List();
    OaVehicle? Get(Guid id);
    void Add(OaVehicle vehicle);
    void Update(OaVehicle vehicle);
}

public interface IOaVehicleUseRequestRepository
{
    IReadOnlyList<OaVehicleUseRequest> List(Guid? applicantUserId = null, Guid? vehicleId = null);
    OaVehicleUseRequest? Get(Guid id);
    void Add(OaVehicleUseRequest request);
    void Update(OaVehicleUseRequest request);
}

public interface IOaVehicleUseWorkflowApprover
{
    void ApplyApproval(OaVehicleUseRequest request);
    void ApplyRejection(OaVehicleUseRequest request, string? reason);
}

public sealed class VehicleService(
    IOaVehicleRepository vehicles,
    IOaVehicleUseRequestRepository requests,
    WorkflowBindingService? bindings = null,
    IWorkflowTransactionBoundary? transactions = null) : IOaVehicleUseWorkflowApprover
{
    public IReadOnlyList<OaVehicle> ListVehicles() => vehicles.List().OrderBy(x => x.Status).ThenBy(x => x.PlateNumber).ToArray();
    public OaVehicle? GetVehicle(Guid id) => id == Guid.Empty ? null : vehicles.Get(id);
    public IReadOnlyList<OaVehicleUseRequest> ListMine(Guid applicantUserId) => applicantUserId == Guid.Empty ? [] : requests.List(applicantUserId).OrderByDescending(x => x.StartAt).ToArray();
    public OaVehicleUseRequest? GetRequest(Guid id) => requests.Get(id);

    public OaVehicle CreateVehicle(string plateNumber, string vehicleType, string brandModel, int seatCount, Guid? responsibleUserId,
        DateOnly? annualInspectionExpiresOn, DateOnly? insuranceExpiresOn, string? otherInfo)
    {
        if (vehicles.List().Any(x => x.PlateNumber.Equals(plateNumber.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("车牌号已存在。");
        var vehicle = new OaVehicle(plateNumber, vehicleType, brandModel, seatCount, responsibleUserId, annualInspectionExpiresOn, insuranceExpiresOn, otherInfo, DateTime.Now);
        vehicles.Add(vehicle);
        return vehicle;
    }

    public void EditVehicle(OaVehicle vehicle, string plateNumber, string vehicleType, string brandModel, int seatCount, Guid? responsibleUserId,
        DateOnly? annualInspectionExpiresOn, DateOnly? insuranceExpiresOn, string? otherInfo)
    {
        if (vehicle.Status == OaVehicleStatus.InUse) throw new InvalidOperationException("使用中的车辆不能编辑台账。");
        if (vehicles.List().Any(x => x.Id != vehicle.Id && x.PlateNumber.Equals(plateNumber.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("车牌号已存在。");
        vehicle.Edit(plateNumber, vehicleType, brandModel, seatCount, responsibleUserId, annualInspectionExpiresOn, insuranceExpiresOn, otherInfo);
        vehicles.Update(vehicle);
    }

    public void SetVehicleStatus(OaVehicle vehicle, OaVehicleStatus status)
    {
        if (status != OaVehicleStatus.InUse && requests.List(vehicleId: vehicle.Id).Any(x => x.Status == OaVehicleUseRequestStatus.Approved))
            throw new InvalidOperationException("车辆存在未归还用车申请，必须先完成归还。");
        vehicle.SetStatus(status);
        vehicles.Update(vehicle);
    }

    public OaVehicleUseRequest CreateRequest(Guid applicantUserId, string applicantName, Guid vehicleId, string driverName,
        DateTime startAt, DateTime endAt, decimal? startMileage, string destination, string purpose, string? otherInfo)
    {
        var vehicle = vehicles.Get(vehicleId) ?? throw new InvalidOperationException("车辆不存在。");
        var request = new OaVehicleUseRequest(vehicle.Id, applicantUserId, applicantName, driverName, startAt, endAt, startMileage, destination, purpose, otherInfo, DateTime.Now);
        requests.Add(request);
        return request;
    }

    public void EditRequest(OaVehicleUseRequest request, Guid actorUserId, string applicantName, Guid vehicleId, string driverName,
        DateTime startAt, DateTime endAt, decimal? startMileage, string destination, string purpose, string? otherInfo)
    {
        EnsureOwner(request, actorUserId);
        EnsureEditable(request);
        _ = vehicles.Get(vehicleId) ?? throw new InvalidOperationException("车辆不存在。");
        request.Edit(applicantName, driverName, startAt, endAt, startMileage, destination, purpose, otherInfo);
        request.SetVehicleForEdit(vehicleId);
        requests.Update(request);
    }

    public void Submit(OaVehicleUseRequest request, Guid actorUserId)
    {
        EnsureOwner(request, actorUserId);
        EnsureSubmitReady(request);
        request.Submit(DateTime.Now);
        requests.Update(request);
    }

    public void SubmitAndStartWorkflow(OaVehicleUseRequest request, Guid actorUserId, string startedBy)
    {
        EnsureOwner(request, actorUserId);
        if (bindings is null) throw new InvalidOperationException("用车审批服务未配置。");
        EnsureSubmitReady(request);
        var previousStatus = request.Status;
        void Core()
        {
            request.Submit(DateTime.Now);
            requests.Update(request);
            bindings.StartOrGet(WorkflowBindingCodes.VehicleUseApproval, nameof(OaVehicleUseRequest), request.Id, startedBy: startedBy);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => request.SetStatus(previousStatus));
    }

    public void Cancel(OaVehicleUseRequest request, Guid actorUserId, string actor)
    {
        EnsureOwner(request, actorUserId);
        var running = bindings?.List(nameof(OaVehicleUseRequest), request.Id).SingleOrDefault(x => x.Status == WorkflowInstanceStatus.Running);
        var previousStatus = request.Status;
        void Core()
        {
            if (running is not null) bindings!.Withdraw(running.Id, actor, "申请人撤回用车申请");
            request.Cancel();
            requests.Update(request);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => request.SetStatus(previousStatus));
    }

    public void ApplyApproval(OaVehicleUseRequest request)
    {
        if (request.Status == OaVehicleUseRequestStatus.Approved) return;
        var vehicle = vehicles.Get(request.VehicleId) ?? throw new InvalidOperationException("流程关联的车辆不存在或已被删除。");
        if (vehicle.Status != OaVehicleStatus.Available) throw new InvalidOperationException("车辆当前不可用，不能批准用车申请。");
        if (requests.List(vehicleId: request.VehicleId).Any(x => x.Id != request.Id && x.Status is (OaVehicleUseRequestStatus.Submitted or OaVehicleUseRequestStatus.Approved) && x.Overlaps(request.StartAt, request.EndAt)))
            throw new InvalidOperationException("该车辆时间段已有提交中或已批准的用车申请。");
        vehicle.MarkInUse();
        request.Approve();
        vehicles.Update(vehicle);
        requests.Update(request);
    }

    public void ApplyRejection(OaVehicleUseRequest request, string? reason)
    {
        if (request.Status == OaVehicleUseRequestStatus.Rejected) return;
        request.Reject(reason);
        requests.Update(request);
    }

    public void Return(OaVehicleUseRequest request, Guid actorUserId, decimal? endMileage)
    {
        EnsureOwner(request, actorUserId);
        var vehicle = vehicles.Get(request.VehicleId) ?? throw new InvalidOperationException("车辆不存在。");
        var previousStatus = request.Status;
        var previousVehicleStatus = vehicle.Status;
        void Core()
        {
            request.Return(endMileage, DateTime.Now);
            vehicle.MarkAvailable();
            requests.Update(request);
            vehicles.Update(vehicle);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => { request.SetStatus(previousStatus); vehicle.SetStatus(previousVehicleStatus); });
    }

    private void EnsureSubmitReady(OaVehicleUseRequest request)
    {
        EnsureEditableOrRejected(request);
        var vehicle = vehicles.Get(request.VehicleId) ?? throw new InvalidOperationException("车辆不存在。");
        if (vehicle.Status != OaVehicleStatus.Available) throw new InvalidOperationException("只有可用车辆才能提交用车申请。");
        if (requests.List(vehicleId: request.VehicleId).Any(x => x.Id != request.Id && x.Status is (OaVehicleUseRequestStatus.Submitted or OaVehicleUseRequestStatus.Approved) && x.Overlaps(request.StartAt, request.EndAt)))
            throw new InvalidOperationException("该车辆时间段已有提交中或已批准的用车申请。");
    }

    private static void EnsureOwner(OaVehicleUseRequest request, Guid actorUserId) { if (actorUserId == Guid.Empty || request.ApplicantUserId != actorUserId) throw new UnauthorizedAccessException("当前用户不能操作其他员工的用车申请。"); }
    private static void EnsureEditable(OaVehicleUseRequest request) { if (request.Status is not (OaVehicleUseRequestStatus.Draft or OaVehicleUseRequestStatus.Rejected)) throw new InvalidOperationException("只有草稿或已驳回用车申请可以编辑。"); }
    private static void EnsureEditableOrRejected(OaVehicleUseRequest request) { if (request.Status is not (OaVehicleUseRequestStatus.Draft or OaVehicleUseRequestStatus.Rejected)) throw new InvalidOperationException("当前状态不能提交用车申请。"); }
}

internal static class OaVehicleRecoveryExtensions
{
    public static void SetStatusForRecovery(this OaVehicleUseRequest request, OaVehicleUseRequestStatus status) => request.SetStatus(status);
}
