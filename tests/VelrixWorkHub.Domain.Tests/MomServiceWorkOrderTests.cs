using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Application.SalesOrders;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class MomServiceWorkOrderTests
{
    [Fact]
    public void InstallationWorkOrder_StartAndComplete_ActivatesEquipment()
    {
        var fixture = Fixture.Create();
        var item = fixture.WorkOrders.CreateInstallation(fixture.Equipment.Id, "MOM-SVC-001", DateOnly.FromDateTime(DateTime.Today), "现场工程师", "客户一厂 A 区", "安装并完成现场调试", "admin");

        Assert.Equal(MomServiceWorkOrderStatus.Scheduled, item.Status);
        fixture.WorkOrders.Start(item.Id, "现场工程师");
        fixture.WorkOrders.CompleteInstallation(item.Id, "现场工程师", DateOnly.FromDateTime(DateTime.Today), "调试通过");

        Assert.Equal(MomServiceWorkOrderStatus.Completed, item.Status);
        Assert.Equal(MomServiceEquipmentStatus.Active, fixture.Equipment.Status);
        Assert.Equal("客户一厂 A 区", fixture.Equipment.InstallationLocation);
        Assert.Equal(4, fixture.WorkOrders.ListHistory(item.Id).Count);
        Assert.Equal(MomServiceWorkOrderHistoryAction.Completed, fixture.WorkOrders.ListHistory(item.Id).First().Action);
    }

    [Fact]
    public void InstallationWorkOrder_RejectsDuplicateOpenOrder_ButAllowsAfterCancellation()
    {
        var fixture = Fixture.Create();
        var first = fixture.WorkOrders.CreateInstallation(fixture.Equipment.Id, "MOM-SVC-002", DateOnly.FromDateTime(DateTime.Today), "工程师", "客户二厂", "安装", "admin");

        Assert.Throws<InvalidOperationException>(() => fixture.WorkOrders.CreateInstallation(fixture.Equipment.Id, "MOM-SVC-003", DateOnly.FromDateTime(DateTime.Today), "工程师", "客户二厂", "重复安装", "admin"));
        fixture.WorkOrders.Cancel(first.Id, "admin", "客户改期");
        var second = fixture.WorkOrders.CreateInstallation(fixture.Equipment.Id, "MOM-SVC-003", DateOnly.FromDateTime(DateTime.Today), "工程师", "客户二厂", "改期安装", "admin");

        Assert.Equal(MomServiceWorkOrderStatus.Cancelled, first.Status);
        Assert.Equal(MomServiceWorkOrderStatus.Scheduled, second.Status);
    }

    [Fact]
    public void InstallationWorkOrder_EnforcesLifecycleOrder()
    {
        var fixture = Fixture.Create();
        var item = fixture.WorkOrders.CreateInstallation(fixture.Equipment.Id, "MOM-SVC-004", DateOnly.FromDateTime(DateTime.Today), "工程师", "客户三厂", "安装", "admin");

        Assert.Throws<InvalidOperationException>(() => fixture.WorkOrders.CompleteInstallation(item.Id, "工程师", DateOnly.FromDateTime(DateTime.Today)));
        fixture.WorkOrders.Cancel(item.Id, "admin", "取消安装");
        Assert.Throws<InvalidOperationException>(() => fixture.WorkOrders.Start(item.Id, "工程师"));
        Assert.Equal(MomServiceEquipmentStatus.PendingInstallation, fixture.Equipment.Status);
    }

    [Fact]
    public void RepairWorkOrder_StartAndComplete_KeepsActiveEquipment()
    {
        var fixture = Fixture.Create();
        fixture.EquipmentService.Install(fixture.Equipment.Id, "installer", DateOnly.FromDateTime(DateTime.Today), "客户现场");
        var item = fixture.WorkOrders.CreateRepair(fixture.Equipment.Id, "MOM-SVC-006", DateOnly.FromDateTime(DateTime.Today), "维修工程师", "客户现场", "设备报警排查", "admin");

        fixture.WorkOrders.Start(item.Id, "维修工程师");
        fixture.WorkOrders.CompleteRepair(item.Id, "维修工程师", "更换传感器后恢复");

        Assert.Equal(MomServiceWorkOrderType.Repair, item.Type);
        Assert.Equal(MomServiceWorkOrderStatus.Completed, item.Status);
        Assert.Equal(MomServiceEquipmentStatus.Active, fixture.Equipment.Status);
        Assert.Equal(4, fixture.WorkOrders.ListHistory(item.Id).Count);
    }

    [Fact]
    public void RepairWorkOrder_RequiresActiveEquipmentAndSharesOpenOrderGate()
    {
        var fixture = Fixture.Create();
        Assert.Throws<InvalidOperationException>(() => fixture.WorkOrders.CreateRepair(fixture.Equipment.Id, "MOM-SVC-007", DateOnly.FromDateTime(DateTime.Today), "维修工程师", "客户现场", "维修", "admin"));

        fixture.EquipmentService.Install(fixture.Equipment.Id, "installer", DateOnly.FromDateTime(DateTime.Today), "客户现场");
        var first = fixture.WorkOrders.CreateRepair(fixture.Equipment.Id, "MOM-SVC-007", DateOnly.FromDateTime(DateTime.Today), "维修工程师", "客户现场", "维修", "admin");
        Assert.Throws<InvalidOperationException>(() => fixture.WorkOrders.CreateRepair(fixture.Equipment.Id, "MOM-SVC-008", DateOnly.FromDateTime(DateTime.Today), "维修工程师", "客户现场", "重复维修", "admin"));

        fixture.WorkOrders.Cancel(first.Id, "admin", "客户改期");
        Assert.Equal(MomServiceWorkOrderStatus.Cancelled, first.Status);
        Assert.Equal(MomServiceEquipmentStatus.Active, fixture.Equipment.Status);
    }

    [Fact]
    public void RepairWorkOrder_UsesRepairSpecificValidationMessages()
    {
        var fixture = Fixture.Create();
        fixture.EquipmentService.Install(fixture.Equipment.Id, "installer", DateOnly.FromDateTime(DateTime.Today), "客户现场");

        var locationError = Assert.Throws<ArgumentException>(() => fixture.WorkOrders.CreateRepair(
            fixture.Equipment.Id, "MOM-SVC-009", DateOnly.FromDateTime(DateTime.Today), "维修工程师", "", "维修", "admin"));
        Assert.Contains("维修现场位置不能为空。", locationError.Message);

        var dateError = Assert.Throws<ArgumentException>(() => fixture.WorkOrders.CreateRepair(
            fixture.Equipment.Id, "MOM-SVC-010", DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), "维修工程师", "客户现场", "维修", "admin"));
        Assert.Contains("计划维修日期不能早于工单创建日期。", dateError.Message);
    }

    [Fact]
    public void CompletionFailure_RestoresWorkOrderAndEquipmentState()
    {
        var boundary = new ThrowOnInnerCallBoundary();
        var fixture = Fixture.Create(boundary);
        var item = fixture.WorkOrders.CreateInstallation(fixture.Equipment.Id, "MOM-SVC-005", DateOnly.FromDateTime(DateTime.Today), "工程师", "客户四厂", "安装", "admin");
        fixture.WorkOrders.Start(item.Id, "工程师");

        Assert.Throws<InvalidOperationException>(() => fixture.WorkOrders.CompleteInstallation(item.Id, "工程师", DateOnly.FromDateTime(DateTime.Today)));
        Assert.Equal(MomServiceWorkOrderStatus.InProgress, item.Status);
        Assert.Equal(MomServiceEquipmentStatus.PendingInstallation, fixture.Equipment.Status);
        Assert.Equal(3, fixture.WorkOrders.ListHistory(item.Id).Count);
    }

    private sealed class Fixture
    {
        public MomServiceEquipment Equipment { get; private init; } = null!;
        public MomServiceEquipmentService EquipmentService { get; private init; } = null!;
        public MomServiceWorkOrderService WorkOrders { get; private init; } = null!;

        public static Fixture Create(IWorkflowTransactionBoundary? boundary = null)
        {
            var customerId = Guid.CreateVersion7(); var productId = Guid.CreateVersion7();
            var order = new SalesOrder("SO-SVC-001", customerId, productId, DateOnly.FromDateTime(DateTime.Today), 1m, 100m);
            order.SetStatus(SalesOrderStatus.Submitted);
            order.SetStatus(SalesOrderStatus.Shipped);
            var shipment = new MomFinishedGoodsShipment(order.Id, Guid.CreateVersion7(), productId, Guid.CreateVersion7(), null, 1m, "SO-SVC-001-OUT", DateOnly.FromDateTime(DateTime.Today), serialNo: "SN-SVC-001");
            var equipmentRepository = new EquipmentRepository(); var lifecycleRepository = new LifecycleRepository();
            var equipmentService = new MomServiceEquipmentService(equipmentRepository, lifecycleRepository,
                new ShipmentRepository([shipment]), new AllocationRepository(), new SalesOrderRepository([order]), new ProjectRepository(), boundary);
            var equipment = equipmentService.CreateFromShipment(shipment.Id, "SN-SVC-001", "admin");
            return new Fixture
            {
                Equipment = equipment,
                EquipmentService = equipmentService,
                WorkOrders = new MomServiceWorkOrderService(new WorkOrderRepository(), new HistoryRepository(), equipmentService, boundary)
            };
        }
    }

    private sealed class EquipmentRepository : IMomServiceEquipmentRepository
    {
        private readonly List<MomServiceEquipment> items = [];
        public IReadOnlyList<MomServiceEquipment> List() => items;
        public void Add(MomServiceEquipment item) => items.Add(item);
        public void Update(MomServiceEquipment item) { }
    }
    private sealed class LifecycleRepository : IMomServiceEquipmentLifecycleRepository
    {
        private readonly List<MomServiceEquipmentLifecycleEntry> items = [];
        public IReadOnlyList<MomServiceEquipmentLifecycleEntry> List(Guid? equipmentId = null) => equipmentId is Guid id ? items.Where(x => x.EquipmentId == id).ToArray() : items;
        public void Add(MomServiceEquipmentLifecycleEntry item) => items.Add(item);
    }
    private sealed class WorkOrderRepository : IMomServiceWorkOrderRepository
    {
        private readonly List<MomServiceWorkOrder> items = [];
        public IReadOnlyList<MomServiceWorkOrder> List(Guid? equipmentId = null) => equipmentId is Guid id ? items.Where(x => x.EquipmentId == id).ToArray() : items;
        public MomServiceWorkOrder? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public void Add(MomServiceWorkOrder item) => items.Add(item);
        public void Update(MomServiceWorkOrder item) { }
    }
    private sealed class HistoryRepository : IMomServiceWorkOrderHistoryRepository
    {
        private readonly List<MomServiceWorkOrderHistory> items = [];
        public IReadOnlyList<MomServiceWorkOrderHistory> List(Guid workOrderId) => items.Where(x => x.WorkOrderId == workOrderId).ToArray();
        public void Add(MomServiceWorkOrderHistory item) => items.Add(item);
    }
    private sealed class ShipmentRepository(IReadOnlyList<MomFinishedGoodsShipment> seed) : IMomFinishedGoodsShipmentRepository
    {
        private readonly List<MomFinishedGoodsShipment> items = seed.ToList();
        public IReadOnlyList<MomFinishedGoodsShipment> List() => items;
        public void Add(MomFinishedGoodsShipment item) => items.Add(item);
    }
    private sealed class AllocationRepository : IMomFinishedGoodsShipmentAllocationRepository
    {
        public IReadOnlyList<MomFinishedGoodsShipmentAllocation> List(Guid? shipmentId = null) => [];
        public void Add(MomFinishedGoodsShipmentAllocation item) { }
    }
    private sealed class SalesOrderRepository(IReadOnlyList<SalesOrder> seed) : ISalesOrderRepository
    {
        private readonly List<SalesOrder> items = seed.ToList();
        public IReadOnlyList<SalesOrder> List() => items;
        public void Add(SalesOrder item) => items.Add(item);
        public void Update(SalesOrder item) { }
    }
    private sealed class ProjectRepository : IPmsProjectRepository
    {
        public IReadOnlyList<PmsProject> List() => [];
        public void Add(PmsProject item) { }
        public void Update(PmsProject item) { }
        public void Remove(Guid id) { }
    }
    private sealed class ThrowOnInnerCallBoundary : IWorkflowTransactionBoundary
    {
        private int depth;
        public void Execute(Action operation, Action<Exception>? afterRollback = null)
        {
            depth++;
            try
            {
                operation();
                if (depth != 2) return;
                var error = new InvalidOperationException("模拟安装工单事务失败。 ");
                afterRollback?.Invoke(error);
                throw error;
            }
            catch (Exception error)
            {
                if (depth == 1) afterRollback?.Invoke(error);
                throw;
            }
            finally { depth--; }
        }
    }
}
