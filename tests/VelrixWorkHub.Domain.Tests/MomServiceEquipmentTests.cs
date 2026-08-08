using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Application.SalesOrders;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class MomServiceEquipmentTests
{
    [Fact]
    public void CreateFromShipment_RequiresShippedOrderAndMatchingSerial()
    {
        var pending = Fixture.Create(shipped: false, shipmentSerial: "SN-001");
        Assert.Throws<InvalidOperationException>(() => pending.Service.CreateFromShipment(pending.Shipment.Id, "SN-001", "service-user"));

        var shipped = Fixture.Create(shipped: true, shipmentSerial: "SN-001");
        var equipment = shipped.Service.CreateFromShipment(shipped.Shipment.Id, "SN-001", "service-user");

        Assert.Equal(MomServiceEquipmentStatus.PendingInstallation, equipment.Status);
        Assert.Equal(shipped.Order.CustomerId, equipment.CustomerId);
        Assert.Equal(shipped.Shipment.SourceNo, equipment.ShipmentSourceNo);
        Assert.Single(shipped.Service.ListLifecycle(equipment.Id));
    }

    [Fact]
    public void CreateFromShipment_UsesMatchingMultiSourceAllocationAndRejectsWrongSerial()
    {
        var fixture = Fixture.Create(shipped: true, shipmentSerial: null, allocations: true);
        var equipment = fixture.Service.CreateFromShipment(fixture.Shipment.Id, "SN-B", "service-user");

        Assert.Equal("SO-EQUIP-001-OUT-A02", equipment.ShipmentSourceNo);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.CreateFromShipment(fixture.Shipment.Id, "SN-X", "service-user", sourceNo: "SO-EQUIP-001-OUT-A01"));
    }

    [Fact]
    public void InstallAndRetire_AdvanceStateAndKeepLifecycleHistory()
    {
        var fixture = Fixture.Create(shipped: true, shipmentSerial: "SN-002");
        var equipment = fixture.Service.CreateFromShipment(fixture.Shipment.Id, "SN-002", "service-user");

        fixture.Service.Install(equipment.Id, "installer", DateOnly.FromDateTime(DateTime.Today), "客户一厂 A 区");
        fixture.Service.Retire(equipment.Id, "service-manager", "设备达到报废年限");

        Assert.Equal(MomServiceEquipmentStatus.Retired, equipment.Status);
        Assert.Equal("客户一厂 A 区", equipment.InstallationLocation);
        Assert.Equal("设备达到报废年限", equipment.RetiredReason);
        Assert.Equal(3, fixture.Service.ListLifecycle(equipment.Id).Count);
        Assert.Equal(MomServiceEquipmentLifecycleAction.Retired, fixture.Service.ListLifecycle(equipment.Id).First().Action);
    }

    [Fact]
    public void CreateFromShipment_RejectsDuplicateIdentityAndRetirementRequiresActive()
    {
        var fixture = Fixture.Create(shipped: true, shipmentSerial: "SN-003");
        var first = fixture.Service.CreateFromShipment(fixture.Shipment.Id, "SN-003", "service-user", equipmentNo: "EQ-003");
        Assert.Throws<InvalidOperationException>(() => fixture.Service.CreateFromShipment(fixture.Shipment.Id, "SN-003", "service-user", equipmentNo: "EQ-004"));
        Assert.Throws<InvalidOperationException>(() => fixture.Service.CreateFromShipment(fixture.Shipment.Id, "SN-004", "service-user", equipmentNo: "EQ-003"));
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Retire(first.Id, "service-manager", "提前报废"));
    }

    [Fact]
    public void InstallTransactionFailure_RestoresPendingState()
    {
        var fixture = Fixture.Create(shipped: true, shipmentSerial: "SN-005", transactions: new ThrowingTransactionBoundary());
        var equipment = fixture.Service.CreateFromShipment(fixture.Shipment.Id, "SN-005", "service-user");

        Assert.Throws<InvalidOperationException>(() => fixture.Service.Install(equipment.Id, "installer", DateOnly.FromDateTime(DateTime.Today), "客户二厂"));
        Assert.Equal(MomServiceEquipmentStatus.PendingInstallation, equipment.Status);
        Assert.Null(equipment.InstallationLocation);
    }

    private sealed class Fixture
    {
        public SalesOrder Order { get; private init; } = null!;
        public MomFinishedGoodsShipment Shipment { get; private init; } = null!;
        public MomServiceEquipmentService Service { get; private init; } = null!;

        public static Fixture Create(bool shipped, string? shipmentSerial, bool allocations = false, IWorkflowTransactionBoundary? transactions = null)
        {
            var customerId = Guid.CreateVersion7(); var productId = Guid.CreateVersion7(); var order = new SalesOrder("SO-EQUIP-001", customerId, productId, DateOnly.FromDateTime(DateTime.Today), 1m, 100m);
            order.SetStatus(SalesOrderStatus.Submitted);
            var shipment = new MomFinishedGoodsShipment(order.Id, Guid.CreateVersion7(), productId, Guid.CreateVersion7(), null, 1m, "SO-EQUIP-001-OUT", DateOnly.FromDateTime(DateTime.Today), serialNo: shipmentSerial);
            if (shipped) order.SetStatus(SalesOrderStatus.Shipped);
            var shipments = new ShipmentRepository([shipment]); var allocationRepository = new AllocationRepository();
            if (allocations)
            {
                allocationRepository.Add(new MomFinishedGoodsShipmentAllocation(shipment.Id, Guid.CreateVersion7(), productId, Guid.CreateVersion7(), null, 1m, "SO-EQUIP-001-OUT-A01", shipment.ShipmentDate, serialNo: "SN-A"));
                allocationRepository.Add(new MomFinishedGoodsShipmentAllocation(shipment.Id, Guid.CreateVersion7(), productId, Guid.CreateVersion7(), null, 1m, "SO-EQUIP-001-OUT-A02", shipment.ShipmentDate, serialNo: "SN-B"));
            }
            var equipmentRepository = new EquipmentRepository(); var lifecycleRepository = new LifecycleRepository();
            return new Fixture
            {
                Order = order, Shipment = shipment,
                Service = new MomServiceEquipmentService(equipmentRepository, lifecycleRepository, shipments, allocationRepository,
                    new SalesOrderRepository([order]), new ProjectRepository(), transactions)
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
    private sealed class ShipmentRepository(IReadOnlyList<MomFinishedGoodsShipment> seed) : IMomFinishedGoodsShipmentRepository
    {
        private readonly List<MomFinishedGoodsShipment> items = seed.ToList();
        public IReadOnlyList<MomFinishedGoodsShipment> List() => items;
        public void Add(MomFinishedGoodsShipment item) => items.Add(item);
    }
    private sealed class AllocationRepository : IMomFinishedGoodsShipmentAllocationRepository
    {
        private readonly List<MomFinishedGoodsShipmentAllocation> items = [];
        public IReadOnlyList<MomFinishedGoodsShipmentAllocation> List(Guid? shipmentId = null) => shipmentId is Guid id ? items.Where(x => x.ShipmentId == id).ToArray() : items;
        public void Add(MomFinishedGoodsShipmentAllocation item) => items.Add(item);
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
    private sealed class ThrowingTransactionBoundary : IWorkflowTransactionBoundary
    {
        private int calls;
        public void Execute(Action operation, Action<Exception>? afterRollback = null)
        {
            operation();
            if (++calls != 2) return;
            var error = new InvalidOperationException("模拟售后设备事务失败。"); afterRollback?.Invoke(error); throw error;
        }
    }
}
