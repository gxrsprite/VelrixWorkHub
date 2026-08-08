using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.Warehouses;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class MomMaterialKittingTests
{
    [Fact]
    public void ReleasedBomCreatesFrozenRequirementsAndChecksWarehouseAvailability()
    {
        var fixture = Fixture.Create(openingQuantity: 12m, plannedQuantity: 5m);

        var snapshot = fixture.Service.CheckKitting(fixture.WorkOrder.Id, fixture.Warehouse.Id);
        var line = Assert.Single(snapshot.Lines);

        Assert.Equal(11m, line.RequiredQuantity);
        Assert.Equal(12m, line.AvailableQuantity);
        Assert.Equal(0m, line.ShortageQuantity);
        Assert.True(snapshot.IsReady);
        Assert.Single(fixture.Requirements.List());
        Assert.Single(fixture.Service.EnsureRequirements(fixture.WorkOrder.Id));
    }

    [Fact]
    public void IssueAndReturnUseInventoryApplicationAndKeepMomMovementTrace()
    {
        var fixture = Fixture.Create(openingQuantity: 12m, plannedQuantity: 5m);
        var requirement = Assert.Single(fixture.Service.EnsureRequirements(fixture.WorkOrder.Id));

        var issue = fixture.Service.Issue(requirement.Id, fixture.Warehouse.Id, null, 4m, DateOnly.FromDateTime(DateTime.Today));
        Assert.Equal(MomMaterialMovementKind.Issue, issue.Kind);
        Assert.StartsWith("MOI-", issue.SourceNo, StringComparison.Ordinal);
        Assert.Equal(8m, fixture.InventoryService.Balances().Single().Quantity);
        Assert.Equal(4m, requirement.NetIssuedQuantity);

        var returned = fixture.Service.Return(requirement.Id, fixture.Warehouse.Id, null, 1m, DateOnly.FromDateTime(DateTime.Today));
        Assert.Equal(MomMaterialMovementKind.Return, returned.Kind);
        Assert.StartsWith("MOR-", returned.SourceNo, StringComparison.Ordinal);
        Assert.Equal(9m, fixture.InventoryService.Balances().Single().Quantity);
        Assert.Equal(3m, requirement.NetIssuedQuantity);
        Assert.Equal(2, fixture.Service.ListMovements(fixture.WorkOrder.Id).Count);
    }

    [Fact]
    public void KittingRejectsShortageOverIssueAndOverReturnWithoutInventoryWrite()
    {
        var fixture = Fixture.Create(openingQuantity: 3m, plannedQuantity: 5m);
        var snapshot = fixture.Service.CheckKitting(fixture.WorkOrder.Id, fixture.Warehouse.Id);
        var requirement = Assert.Single(fixture.Service.ListRequirements(fixture.WorkOrder.Id));

        Assert.False(snapshot.IsReady);
        Assert.Equal(8m, snapshot.Lines.Single().ShortageQuantity);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Issue(requirement.Id, fixture.Warehouse.Id, null, 4m, DateOnly.FromDateTime(DateTime.Today)));
        Assert.Empty(fixture.Service.ListMovements(fixture.WorkOrder.Id));
        Assert.Equal(3m, fixture.InventoryService.Balances().Single().Quantity);

        var issue = fixture.Service.Issue(requirement.Id, fixture.Warehouse.Id, null, 2m, DateOnly.FromDateTime(DateTime.Today));
        Assert.NotNull(issue);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Return(requirement.Id, fixture.Warehouse.Id, null, 3m, DateOnly.FromDateTime(DateTime.Today)));
        Assert.Equal(1m, fixture.InventoryService.Balances().Single().Quantity);
        Assert.Single(fixture.Service.ListMovements(fixture.WorkOrder.Id));
    }

    [Fact]
    public void DraftWorkOrderCannotCreateOrIssueMaterial()
    {
        var fixture = Fixture.Create(openingQuantity: 12m, plannedQuantity: 5m, releaseWorkOrder: false);

        var error = Assert.Throws<InvalidOperationException>(() => fixture.Service.CheckKitting(fixture.WorkOrder.Id, fixture.Warehouse.Id));

        Assert.Contains("只有已下达", error.Message);
        Assert.Empty(fixture.Requirements.List());
        Assert.Single(fixture.InventoryRepository.List());
    }

    [Fact]
    public void DeliveryAndConsumptionFollowIssuedMaterialWithoutSecondInventoryWrite()
    {
        var fixture = Fixture.Create(openingQuantity: 12m, plannedQuantity: 5m);
        var requirement = Assert.Single(fixture.Service.EnsureRequirements(fixture.WorkOrder.Id));
        fixture.Service.Issue(requirement.Id, fixture.Warehouse.Id, null, 5m, DateOnly.FromDateTime(DateTime.Today));

        var delivery = fixture.Service.Deliver(requirement.Id, fixture.WorkCenter.Id, 3m, DateOnly.FromDateTime(DateTime.Today));
        Assert.StartsWith("MOD-", delivery.SourceNo, StringComparison.Ordinal);
        var delivered = fixture.Service.CheckKitting(fixture.WorkOrder.Id, fixture.Warehouse.Id).Lines.Single();
        Assert.Equal(3m, delivered.DeliveredQuantity);
        Assert.Equal(3m, delivered.RemainingToConsume);

        fixture.WorkOrder.SetStatus(MomWorkOrderStatus.InProgress);
        var consumption = fixture.Service.Consume(requirement.Id, fixture.WorkCenter.Id, 2m, DateOnly.FromDateTime(DateTime.Today), deliveryId: delivery.Id);
        Assert.StartsWith("MOC-", consumption.SourceNo, StringComparison.Ordinal);
        Assert.Equal(delivery.Id, consumption.DeliveryId);
        var consumed = fixture.Service.CheckKitting(fixture.WorkOrder.Id, fixture.Warehouse.Id).Lines.Single();
        Assert.Equal(2m, consumed.ConsumedQuantity);
        Assert.Equal(1m, consumed.RemainingToConsume);
        Assert.Equal(7m, fixture.InventoryService.Balances().Single().Quantity);
        Assert.Single(fixture.Service.ListDeliveries(fixture.WorkOrder.Id));
        Assert.Single(fixture.Service.ListConsumptions(fixture.WorkOrder.Id));
    }

    [Fact]
    public void ConsumptionCanBeAllocatedToExactDeliveryAndCannotExceedThatDelivery()
    {
        var fixture = Fixture.Create(openingQuantity: 12m, plannedQuantity: 5m);
        var requirement = Assert.Single(fixture.Service.EnsureRequirements(fixture.WorkOrder.Id));
        fixture.Service.Issue(requirement.Id, fixture.Warehouse.Id, null, 5m, DateOnly.FromDateTime(DateTime.Today));
        var first = fixture.Service.Deliver(requirement.Id, fixture.WorkCenter.Id, 2m, DateOnly.FromDateTime(DateTime.Today));
        var second = fixture.Service.Deliver(requirement.Id, fixture.WorkCenter.Id, 3m, DateOnly.FromDateTime(DateTime.Today));
        fixture.WorkOrder.SetStatus(MomWorkOrderStatus.InProgress);

        var firstConsumption = fixture.Service.Consume(requirement.Id, fixture.WorkCenter.Id, 2m, DateOnly.FromDateTime(DateTime.Today), deliveryId: first.Id);
        Assert.Equal(first.Id, firstConsumption.DeliveryId);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Consume(requirement.Id, fixture.WorkCenter.Id, 1m, DateOnly.FromDateTime(DateTime.Today), deliveryId: first.Id));

        var secondConsumption = fixture.Service.Consume(requirement.Id, fixture.WorkCenter.Id, 2m, DateOnly.FromDateTime(DateTime.Today), deliveryId: second.Id);
        Assert.Equal(second.Id, secondConsumption.DeliveryId);
        var line = fixture.Service.CheckKitting(fixture.WorkOrder.Id, fixture.Warehouse.Id).Lines.Single();
        Assert.Equal(4m, line.ConsumedQuantity);
        Assert.Equal(1m, line.RemainingToConsume);
    }

    [Fact]
    public void BatchConsumptionAllocatesAcrossDeliveriesAndReversalRestoresEachSource()
    {
        var fixture = Fixture.Create(openingQuantity: 12m, plannedQuantity: 5m);
        var requirement = Assert.Single(fixture.Service.EnsureRequirements(fixture.WorkOrder.Id));
        fixture.Service.Issue(requirement.Id, fixture.Warehouse.Id, null, 5m, DateOnly.FromDateTime(DateTime.Today));
        var first = fixture.Service.Deliver(requirement.Id, fixture.WorkCenter.Id, 2m, DateOnly.FromDateTime(DateTime.Today), batchNo: "LOT-01");
        var second = fixture.Service.Deliver(requirement.Id, fixture.WorkCenter.Id, 3m, DateOnly.FromDateTime(DateTime.Today), batchNo: "LOT-01");
        fixture.WorkOrder.SetStatus(MomWorkOrderStatus.InProgress);

        Assert.Equal(2, fixture.Service.ListDeliveries(fixture.WorkOrder.Id).Count);

        var consumption = fixture.Service.ConsumeByBatch(requirement.Id, fixture.WorkCenter.Id, "LOT-01", 4m, DateOnly.FromDateTime(DateTime.Today));

        Assert.Null(consumption.DeliveryId);
        Assert.Equal(2, fixture.Service.ListConsumptionAllocations(fixture.WorkOrder.Id).Count);
        Assert.Equal([2m, 2m], fixture.Service.ListConsumptionAllocations(fixture.WorkOrder.Id).Select(x => x.Quantity).OrderBy(x => x).ToArray());
        Assert.Throws<InvalidOperationException>(() => fixture.Service.ConsumeByBatch(requirement.Id, fixture.WorkCenter.Id, "LOT-01", 2m, DateOnly.FromDateTime(DateTime.Today)));

        var reversals = fixture.Service.ReverseConsumption(consumption.Id, 3m, DateOnly.FromDateTime(DateTime.Today));
        Assert.Equal(2, reversals.Count);
        Assert.Equal(3m, reversals.Sum(x => x.Quantity));
        Assert.Equal(1m, fixture.Service.CheckKitting(fixture.WorkOrder.Id, fixture.Warehouse.Id).Lines.Single().ConsumedQuantity);

        Assert.Equal(2m, fixture.Service.ListConsumptionReversals(fixture.WorkOrder.Id).Where(x => x.DeliveryId == first.Id).Sum(x => x.Quantity));
        Assert.Equal(1m, fixture.Service.ListConsumptionReversals(fixture.WorkOrder.Id).Where(x => x.DeliveryId == second.Id).Sum(x => x.Quantity));
        Assert.Throws<InvalidOperationException>(() => fixture.Service.ReverseConsumption(consumption.Id, 2m, DateOnly.FromDateTime(DateTime.Today)));
    }

    [Fact]
    public void DeliveredMaterialCannotBeReturnedAndConsumptionCannotExceedDelivery()
    {
        var fixture = Fixture.Create(openingQuantity: 12m, plannedQuantity: 5m);
        var requirement = Assert.Single(fixture.Service.EnsureRequirements(fixture.WorkOrder.Id));
        fixture.Service.Issue(requirement.Id, fixture.Warehouse.Id, null, 5m, DateOnly.FromDateTime(DateTime.Today));
        fixture.Service.Deliver(requirement.Id, fixture.WorkCenter.Id, 3m, DateOnly.FromDateTime(DateTime.Today));

        Assert.Throws<InvalidOperationException>(() => fixture.Service.Return(requirement.Id, fixture.Warehouse.Id, null, 3m, DateOnly.FromDateTime(DateTime.Today)));
        fixture.WorkOrder.SetStatus(MomWorkOrderStatus.InProgress);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Consume(requirement.Id, fixture.WorkCenter.Id, 4m, DateOnly.FromDateTime(DateTime.Today)));
        Assert.Empty(fixture.Service.ListConsumptions(fixture.WorkOrder.Id));
        Assert.Equal(7m, fixture.InventoryService.Balances().Single().Quantity);
    }

    [Fact]
    public void PhysicalDeliveryTransfersInventoryAndKeepsMomEndpoints()
    {
        var fixture = Fixture.Create(openingQuantity: 12m, plannedQuantity: 5m);
        var requirement = Assert.Single(fixture.Service.EnsureRequirements(fixture.WorkOrder.Id));

        var delivery = fixture.Service.DeliverPhysically(requirement.Id, fixture.WorkCenter.Id, fixture.Warehouse.Id, null,
            fixture.TargetWarehouse.Id, fixture.TargetLocation.Id, 4m, DateOnly.FromDateTime(DateTime.Today));

        Assert.Equal(fixture.Warehouse.Id, delivery.SourceWarehouseId);
        Assert.Equal(fixture.TargetWarehouse.Id, delivery.TargetWarehouseId);
        Assert.Equal(fixture.TargetLocation.Id, delivery.TargetLocationId);
        Assert.Equal(4m, requirement.NetIssuedQuantity);
        Assert.Equal(8m, fixture.InventoryService.Balances().Single(x => x.WarehouseId == fixture.Warehouse.Id).Quantity);
        Assert.Equal(4m, fixture.InventoryService.LocationBalances().Single(x => x.WarehouseId == fixture.TargetWarehouse.Id && x.LocationId == fixture.TargetLocation.Id).Quantity);
        Assert.Equal(3, fixture.InventoryRepository.List().Count);
        Assert.Single(fixture.Service.ListDeliveries(fixture.WorkOrder.Id));
    }

    [Fact]
    public void PhysicalDeliveryCanBeWithdrawnAndTransfersInventoryBack()
    {
        var fixture = Fixture.Create(openingQuantity: 12m, plannedQuantity: 5m);
        var requirement = Assert.Single(fixture.Service.EnsureRequirements(fixture.WorkOrder.Id));
        var delivery = fixture.Service.DeliverPhysically(requirement.Id, fixture.WorkCenter.Id, fixture.Warehouse.Id, null,
            fixture.TargetWarehouse.Id, fixture.TargetLocation.Id, 4m, DateOnly.FromDateTime(DateTime.Today));

        var reversal = fixture.Service.WithdrawDelivery(delivery.Id, 2m, DateOnly.FromDateTime(DateTime.Today));

        Assert.StartsWith("MDR-", reversal.SourceNo, StringComparison.Ordinal);
        Assert.Equal(10m, fixture.InventoryService.Balances().Single(x => x.WarehouseId == fixture.Warehouse.Id).Quantity);
        Assert.Equal(2m, fixture.InventoryService.LocationBalances().Single(x => x.WarehouseId == fixture.TargetWarehouse.Id && x.LocationId == fixture.TargetLocation.Id).Quantity);
        Assert.Equal(2m, requirement.NetIssuedQuantity);
        Assert.Single(fixture.Service.ListDeliveryReversals(fixture.WorkOrder.Id));

        fixture.Service.WithdrawDelivery(delivery.Id, 2m, DateOnly.FromDateTime(DateTime.Today));

        Assert.Equal(12m, fixture.InventoryService.Balances().Single(x => x.WarehouseId == fixture.Warehouse.Id).Quantity);
        Assert.Equal(0m, fixture.InventoryService.LocationBalances().SingleOrDefault(x => x.WarehouseId == fixture.TargetWarehouse.Id && x.LocationId == fixture.TargetLocation.Id)?.Quantity ?? 0m);
        Assert.Equal(0m, requirement.NetIssuedQuantity);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.WithdrawDelivery(delivery.Id, 1m, DateOnly.FromDateTime(DateTime.Today)));
    }

    [Fact]
    public void LogicalDeliveryCannotWithdrawConsumedMaterial()
    {
        var fixture = Fixture.Create(openingQuantity: 12m, plannedQuantity: 5m);
        var requirement = Assert.Single(fixture.Service.EnsureRequirements(fixture.WorkOrder.Id));
        fixture.Service.Issue(requirement.Id, fixture.Warehouse.Id, null, 4m, DateOnly.FromDateTime(DateTime.Today));
        var delivery = fixture.Service.Deliver(requirement.Id, fixture.WorkCenter.Id, 4m, DateOnly.FromDateTime(DateTime.Today));
        fixture.WorkOrder.SetStatus(MomWorkOrderStatus.InProgress);
        fixture.Service.Consume(requirement.Id, fixture.WorkCenter.Id, 2m, DateOnly.FromDateTime(DateTime.Today));

        Assert.Throws<InvalidOperationException>(() => fixture.Service.WithdrawDelivery(delivery.Id, 3m, DateOnly.FromDateTime(DateTime.Today)));
        fixture.Service.WithdrawDelivery(delivery.Id, 2m, DateOnly.FromDateTime(DateTime.Today));
        Assert.Equal(2m, requirement.NetIssuedQuantity);
        Assert.Equal(2m, fixture.Service.CheckKitting(fixture.WorkOrder.Id, fixture.Warehouse.Id).Lines.Single().ConsumedQuantity);
    }

    private sealed class Fixture
    {
        public Product Parent { get; private init; } = null!;
        public Product Component { get; private init; } = null!;
        public MomWorkOrder WorkOrder { get; private init; } = null!;
        public Warehouse Warehouse { get; private init; } = null!;
        public Warehouse TargetWarehouse { get; private init; } = null!;
        public WarehouseLocation TargetLocation { get; private init; } = null!;
        public MomWorkCenter WorkCenter { get; private init; } = null!;
        public InMemoryRequirementRepository Requirements { get; private init; } = null!;
        public InMemoryInventoryRepository InventoryRepository { get; private init; } = null!;
        public InventoryService InventoryService { get; private init; } = null!;
        public MomMaterialKittingService Service { get; private init; } = null!;

        public static Fixture Create(decimal openingQuantity, decimal plannedQuantity, bool releaseWorkOrder = true)
        {
            var parent = new Product("FG-KIT-001", "齐套成品", "套", 100, null);
            var component = new Product("RM-KIT-001", "齐套组件", "件", 10, null);
            var version = new MomManufacturingVersion(parent.Id, "V1.0", "齐套版本", DateOnly.FromDateTime(DateTime.Today));
            var versions = new InMemoryVersionRepository([version]);
            var components = new InMemoryComponentRepository([new MomManufacturingComponent(version.Id, 10, component.Id, 2, 10)]);
            version.Release();
            var factory = new MomFactory("FACT-KIT-001", "齐套工厂");
            var workCenter = new MomWorkCenter(factory.Id, "WC-KIT-001", "齐套工位", MomWorkCenterType.Assembly, 8);
            var workOrder = new MomWorkOrder($"MO-KIT-{Guid.CreateVersion7():N}", parent.Id, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(7)), plannedQuantity);
            if (releaseWorkOrder)
            {
                workOrder.SetWorkCenter(workCenter.Id);
                workOrder.SetStatus(MomWorkOrderStatus.Planned);
                workOrder.SetStatus(MomWorkOrderStatus.Released);
            }
            var workOrders = new InMemoryWorkOrderRepository([workOrder]);
            var warehouse = new Warehouse("WH-KIT-001", "齐套仓", null);
            var targetWarehouse = new Warehouse("WH-LINE-001", "线边仓", null);
            var targetLocation = targetWarehouse.AddLocation("LINE-01", "一号线边位");
            var warehouses = new InMemoryWarehouseRepository([warehouse, targetWarehouse]);
            var inventoryRepository = new InMemoryInventoryRepository();
            if (openingQuantity > 0) inventoryRepository.Add(new InventoryTransaction(component.Id, warehouse.Id, InventoryTransactionKind.Inbound, openingQuantity, "OPENING-KIT", DateOnly.FromDateTime(DateTime.Today), "期初库存"));
            var products = new InMemoryProductRepository([parent, component]);
            var inventoryService = new InventoryService(inventoryRepository, products, warehouses);
            var requirements = new InMemoryRequirementRepository();
            var movements = new InMemoryMovementRepository();
            var deliveries = new InMemoryDeliveryRepository();
            var consumptions = new InMemoryConsumptionRepository();
            var reversals = new InMemoryDeliveryReversalRepository();
            var allocations = new InMemoryConsumptionAllocationRepository();
            var consumptionReversals = new InMemoryConsumptionReversalRepository();
            return new Fixture
            {
                Parent = parent, Component = component, WorkOrder = workOrder, Warehouse = warehouse, TargetWarehouse = targetWarehouse, TargetLocation = targetLocation, WorkCenter = workCenter,
                Requirements = requirements, InventoryRepository = inventoryRepository, InventoryService = inventoryService,
                Service = new MomMaterialKittingService(workOrders, requirements, movements, deliveries, consumptions, reversals, allocations, consumptionReversals, versions, components, new InMemoryWorkCenterRepository([workCenter]), products, warehouses, inventoryService)
            };
        }
    }

    private sealed class InMemoryRequirementRepository(IReadOnlyList<MomWorkOrderMaterialRequirement>? seed = null) : IMomWorkOrderMaterialRequirementRepository
    {
        private readonly List<MomWorkOrderMaterialRequirement> items = seed?.ToList() ?? [];
        public IReadOnlyList<MomWorkOrderMaterialRequirement> List() => items;
        public void Add(MomWorkOrderMaterialRequirement item) => items.Add(item);
        public void Update(MomWorkOrderMaterialRequirement item) { }
    }

    private sealed class InMemoryMovementRepository : IMomMaterialMovementRepository
    {
        private readonly List<MomMaterialMovement> items = [];
        public IReadOnlyList<MomMaterialMovement> List() => items;
        public void Add(MomMaterialMovement item) => items.Add(item);
    }

    private sealed class InMemoryDeliveryRepository : IMomMaterialDeliveryRepository
    {
        private readonly List<MomMaterialDelivery> items = [];
        public IReadOnlyList<MomMaterialDelivery> List() => items;
        public void Add(MomMaterialDelivery item) => items.Add(item);
    }

    private sealed class InMemoryConsumptionRepository : IMomMaterialConsumptionRepository
    {
        private readonly List<MomMaterialConsumption> items = [];
        public IReadOnlyList<MomMaterialConsumption> List() => items;
        public void Add(MomMaterialConsumption item) => items.Add(item);
    }

    private sealed class InMemoryDeliveryReversalRepository : IMomMaterialDeliveryReversalRepository
    {
        private readonly List<MomMaterialDeliveryReversal> items = [];
        public IReadOnlyList<MomMaterialDeliveryReversal> List() => items;
        public void Add(MomMaterialDeliveryReversal item) => items.Add(item);
    }

    private sealed class InMemoryConsumptionAllocationRepository : IMomMaterialConsumptionAllocationRepository
    {
        private readonly List<MomMaterialConsumptionAllocation> items = [];
        public IReadOnlyList<MomMaterialConsumptionAllocation> List() => items;
        public void Add(MomMaterialConsumptionAllocation item) => items.Add(item);
    }

    private sealed class InMemoryConsumptionReversalRepository : IMomMaterialConsumptionReversalRepository
    {
        private readonly List<MomMaterialConsumptionReversal> items = [];
        public IReadOnlyList<MomMaterialConsumptionReversal> List() => items;
        public void Add(MomMaterialConsumptionReversal item) => items.Add(item);
    }

    private sealed class InMemoryVersionRepository(IReadOnlyList<MomManufacturingVersion>? seed = null) : IMomManufacturingVersionRepository
    {
        private readonly List<MomManufacturingVersion> items = seed?.ToList() ?? [];
        public IReadOnlyList<MomManufacturingVersion> List() => items;
        public void Add(MomManufacturingVersion item) => items.Add(item);
        public void Update(MomManufacturingVersion item) { }
    }

    private sealed class InMemoryComponentRepository(IReadOnlyList<MomManufacturingComponent>? seed = null) : IMomManufacturingComponentRepository
    {
        private readonly List<MomManufacturingComponent> items = seed?.ToList() ?? [];
        public IReadOnlyList<MomManufacturingComponent> List() => items;
        public void Add(MomManufacturingComponent item) => items.Add(item);
        public void Update(MomManufacturingComponent item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class InMemoryWorkOrderRepository(IReadOnlyList<MomWorkOrder>? seed = null) : IMomWorkOrderRepository
    {
        private readonly List<MomWorkOrder> items = seed?.ToList() ?? [];
        public IReadOnlyList<MomWorkOrder> List() => items;
        public void Add(MomWorkOrder item) => items.Add(item);
        public void Update(MomWorkOrder item) { }
    }

    private sealed class InMemoryWorkCenterRepository(IReadOnlyList<MomWorkCenter>? seed = null) : IMomWorkCenterRepository
    {
        private readonly List<MomWorkCenter> items = seed?.ToList() ?? [];
        public IReadOnlyList<MomWorkCenter> List() => items;
        public void Add(MomWorkCenter item) => items.Add(item);
        public void Update(MomWorkCenter item) { }
    }

    private sealed class InMemoryInventoryRepository : IInventoryTransactionRepository
    {
        private readonly List<InventoryTransaction> items = [];
        public IReadOnlyList<InventoryTransaction> List() => items;
        public void Add(InventoryTransaction item) => items.Add(item);
    }

    private sealed class InMemoryProductRepository(IReadOnlyList<Product> seed) : IProductRepository
    {
        private readonly List<Product> items = seed.ToList();
        public IReadOnlyList<Product> List() => items;
        public void Add(Product item) => items.Add(item);
        public void Update(Product item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class InMemoryWarehouseRepository(IReadOnlyList<Warehouse> seed) : IWarehouseRepository
    {
        private readonly List<Warehouse> items = seed.ToList();
        public IReadOnlyList<Warehouse> List() => items;
        public void Add(Warehouse item) => items.Add(item);
        public void Update(Warehouse item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
        public void AddLocation(WarehouseLocation item) { }
        public void RemoveLocation(Guid id) { }
        public void UpsertLocationProductCapacity(WarehouseLocationProductCapacity item) { }
        public void RemoveLocationProductCapacity(Guid locationId, Guid productId) { }
    }
}
