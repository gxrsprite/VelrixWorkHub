using VelrixWorkHub.Application.Vehicles;
using VelrixWorkHub.Application.Employees;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class VehicleServiceTests
{
    [Fact]
    public void ApprovalOccupiesVehicleAndReturnReleasesIt()
    {
        var vehicleRepository = new VehicleRepository();
        var requestRepository = new VehicleRequestRepository();
        var service = new VehicleService(vehicleRepository, requestRepository);
        var userId = Guid.CreateVersion7();
        var vehicle = service.CreateVehicle("粤A·10001", "轿车", "Velrix V1", 5, null, null, null, null);
        var request = service.CreateRequest(userId, "申请人", vehicle.Id, "驾驶员", DateTime.Today.AddDays(1).AddHours(9), DateTime.Today.AddDays(1).AddHours(12), 100m, "客户现场", "客户拜访", null);

        service.Submit(request, userId);
        service.ApplyApproval(request);

        Assert.Equal(OaVehicleUseRequestStatus.Approved, request.Status);
        Assert.Equal(OaVehicleStatus.InUse, vehicle.Status);

        service.Return(request, userId, 130m);

        Assert.Equal(OaVehicleUseRequestStatus.Returned, request.Status);
        Assert.Equal(130m, request.EndMileage);
        Assert.Equal(OaVehicleStatus.Available, vehicle.Status);
    }

    [Fact]
    public void SubmittedRequestsCannotOverlapAndMaintenanceVehicleCannotSubmit()
    {
        var vehicleRepository = new VehicleRepository();
        var requestRepository = new VehicleRequestRepository();
        var service = new VehicleService(vehicleRepository, requestRepository);
        var userId = Guid.CreateVersion7();
        var vehicle = service.CreateVehicle("粤A·10002", "客车", "Velrix Bus", 7, null, null, null, null);
        var start = DateTime.Today.AddDays(2).AddHours(9);
        var first = service.CreateRequest(userId, "申请人", vehicle.Id, "驾驶员", start, start.AddHours(3), null, "机场", "接送", null);
        var second = service.CreateRequest(userId, "申请人", vehicle.Id, "驾驶员", start.AddHours(1), start.AddHours(4), null, "车站", "接送", null);

        service.Submit(first, userId);
        Assert.Throws<InvalidOperationException>(() => service.Submit(second, userId));
        service.SetVehicleStatus(vehicle, OaVehicleStatus.Maintenance);
        var third = service.CreateRequest(userId, "申请人", vehicle.Id, "驾驶员", start.AddDays(1), start.AddDays(1).AddHours(2), null, "仓库", "取件", null);
        Assert.Throws<InvalidOperationException>(() => service.Submit(third, userId));
    }

    [Fact]
    public void RejectedRequestCanBeResubmittedAndMileageCannotGoBackwards()
    {
        var vehicleRepository = new VehicleRepository();
        var requestRepository = new VehicleRequestRepository();
        var service = new VehicleService(vehicleRepository, requestRepository);
        var userId = Guid.CreateVersion7();
        var vehicle = service.CreateVehicle("粤A·10003", "轿车", "Velrix V2", 5, null, null, null, null);
        var request = service.CreateRequest(userId, "申请人", vehicle.Id, "驾驶员", DateTime.Today.AddDays(3).AddHours(9), DateTime.Today.AddDays(3).AddHours(10), 100m, "园区", "巡检", null);

        service.Submit(request, userId);
        request.Reject("请补充目的地联系人");
        service.EditRequest(request, userId, "申请人", vehicle.Id, "驾驶员", request.StartAt, request.EndAt, 100m, "园区", "巡检补充", null);
        service.Submit(request, userId);
        service.ApplyApproval(request);

        Assert.Throws<InvalidOperationException>(() => service.Return(request, userId, 99m));
        Assert.Equal(OaVehicleUseRequestStatus.Approved, request.Status);
    }

    [Fact]
    public void MaintenanceOccupiesVehicleUntilReporterCompletesOrCancels()
    {
        var vehicleRepository = new VehicleRepository();
        var requestRepository = new VehicleRequestRepository();
        var maintenanceRepository = new MaintenanceRepository();
        var vehicleService = new VehicleService(vehicleRepository, requestRepository);
        var maintenanceService = new VehicleMaintenanceService(maintenanceRepository, vehicleService);
        var userId = Guid.CreateVersion7();
        var vehicle = vehicleService.CreateVehicle("粤A·10004", "轿车", "Velrix V3", 5, null, null, null, null);

        var maintenance = maintenanceService.Start(vehicle.Id, userId, "alice", DateTime.Now, 120m, "更换制动片", "维修中心", 800m, "{}");
        var request = vehicleService.CreateRequest(userId, "alice", vehicle.Id, "alice", DateTime.Today.AddDays(1).AddHours(9), DateTime.Today.AddDays(1).AddHours(11), null, "园区", "巡检", null);

        Assert.Equal(OaVehicleStatus.Maintenance, vehicle.Status);
        Assert.Throws<InvalidOperationException>(() => vehicleService.Submit(request, userId));
        Assert.Throws<InvalidOperationException>(() => maintenanceService.Start(vehicle.Id, userId, "alice", DateTime.Now, null, "重复维修", null, null, "{}"));
        Assert.Throws<UnauthorizedAccessException>(() => maintenanceService.Complete(maintenance, Guid.CreateVersion7(), "越权完成"));

        maintenanceService.Complete(maintenance, userId, "完成路试，车辆可用。");

        Assert.Equal(OaVehicleMaintenanceStatus.Completed, maintenance.Status);
        Assert.Equal(OaVehicleStatus.Available, vehicle.Status);
        Assert.Equal("完成路试，车辆可用。", maintenance.CompletionNotes);
    }

    [Fact]
    public void ComplianceReminder_NotifiesEnabledResponsibleUserOnceAndSkipsRetiredOrUnassignedVehicles()
    {
        var today = new DateOnly(2026, 7, 22);
        var owner = new EmployeeDirectoryEntry(Guid.CreateVersion7(), "OWNER", "车队负责人", null, null, true, null, null);
        var vehicleRepository = new VehicleRepository();
        var vehicleService = new VehicleService(vehicleRepository, new VehicleRequestRepository());
        vehicleService.CreateVehicle("粤A·10005", "轿车", "Velrix V4", 5, owner.UserId, today.AddDays(30), today.AddDays(-1), "{}");
        var retired = vehicleService.CreateVehicle("粤A·10006", "轿车", "Velrix V5", 5, owner.UserId, today, today, "{}");
        vehicleService.SetVehicleStatus(retired, OaVehicleStatus.Retired);
        vehicleService.CreateVehicle("粤A·10007", "轿车", "Velrix V6", 5, null, today, null, "{}");
        var notifications = new NotificationRepository();
        var service = new VehicleComplianceReminderService(vehicleService, new EmployeeDirectoryService(new DirectoryRepository(owner)), new NotificationService(notifications));

        var first = service.Scan(today);
        var second = service.Scan(today);

        Assert.Equal(1, first.InspectionDueCount);
        Assert.Equal(1, first.InsuranceDueCount);
        Assert.Equal(2, first.NotificationAttemptCount);
        Assert.Equal(2, first.SkippedVehicleCount);
        Assert.Equal(2, notifications.Items.Count);
        Assert.All(notifications.Items, item => Assert.Equal("owner", item.Recipient));
        Assert.Equal(2, second.NotificationAttemptCount);
        Assert.Equal(2, notifications.Items.Count);
    }

    private sealed class VehicleRepository : IOaVehicleRepository
    {
        private readonly List<OaVehicle> items = [];
        public IReadOnlyList<OaVehicle> List() => items;
        public OaVehicle? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public void Add(OaVehicle vehicle) => items.Add(vehicle);
        public void Update(OaVehicle vehicle) { }
    }

    private sealed class VehicleRequestRepository : IOaVehicleUseRequestRepository
    {
        private readonly List<OaVehicleUseRequest> items = [];
        public IReadOnlyList<OaVehicleUseRequest> List(Guid? applicantUserId = null, Guid? vehicleId = null) => items.Where(x => (applicantUserId is null || x.ApplicantUserId == applicantUserId) && (vehicleId is null || x.VehicleId == vehicleId)).ToArray();
        public OaVehicleUseRequest? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public void Add(OaVehicleUseRequest request) => items.Add(request);
        public void Update(OaVehicleUseRequest request) { }
    }

    private sealed class MaintenanceRepository : IOaVehicleMaintenanceRepository
    {
        private readonly List<OaVehicleMaintenance> items = [];
        public IReadOnlyList<OaVehicleMaintenance> List(Guid? vehicleId = null) => vehicleId is Guid id ? items.Where(item => item.VehicleId == id).ToArray() : items;
        public OaVehicleMaintenance? Get(Guid id) => items.FirstOrDefault(item => item.Id == id);
        public void Add(OaVehicleMaintenance item) => items.Add(item);
        public void Update(OaVehicleMaintenance item) { }
    }

    private sealed class DirectoryRepository(params EmployeeDirectoryEntry[] data) : IEmployeeDirectoryRepository
    {
        public IReadOnlyList<EmployeeDirectoryEntry> List() => data;
        public IReadOnlyList<EmployeeDirectoryOrganization> ListOrganizations() => [];
    }

    private sealed class NotificationRepository : INotificationRepository
    {
        public List<WorkNotification> Items { get; } = [];
        public IReadOnlyList<WorkNotification> List(string recipient, bool unreadOnly = false) => Items.Where(item => item.Recipient.Equals(recipient, StringComparison.OrdinalIgnoreCase)).ToArray();
        public WorkNotification? FindByDedupeKey(string recipient, string dedupeKey) => Items.FirstOrDefault(item => item.Recipient.Equals(recipient, StringComparison.OrdinalIgnoreCase) && item.DedupeKey == dedupeKey);
        public void Add(WorkNotification notification) => Items.Add(notification);
        public bool TryAdd(WorkNotification notification) { if (FindByDedupeKey(notification.Recipient, notification.DedupeKey) is not null) return false; Items.Add(notification); return true; }
        public void Update(WorkNotification notification) { }
        public int Delete(string recipient, IReadOnlyCollection<Guid> notificationIds) => 0;
    }
}
