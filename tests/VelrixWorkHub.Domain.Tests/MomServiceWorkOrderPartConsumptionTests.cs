using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.Warehouses;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class MomServiceWorkOrderPartConsumptionTests
{
    [Fact]
    public void RepairPartConsumption_CreatesOutboundSnapshotAndIsIdempotent()
    {
        var fixture = Fixture.Create();

        var first = fixture.Consumptions.Create(fixture.WorkOrder.Id, fixture.Product.Id, fixture.Warehouse.Id,
            fixture.Location.Id, 2m, "MOM-REP-001", DateOnly.FromDateTime(DateTime.Today), "维修工程师", "B-001", null, null, "更换风扇");
        var retry = fixture.Consumptions.Create(fixture.WorkOrder.Id, fixture.Product.Id, fixture.Warehouse.Id,
            fixture.Location.Id, 2m, "MOM-REP-001", DateOnly.FromDateTime(DateTime.Today), "维修工程师", "B-001", null, null, "更换风扇");

        Assert.Same(first, retry);
        Assert.Single(fixture.Consumptions.List(fixture.WorkOrder.Id));
        Assert.Equal(2, fixture.Inventory.List().Count);
        Assert.Equal(3m, fixture.InventoryService.LocationBalances().Single().Quantity);
        Assert.Equal("B-001", first.BatchNo);
        Assert.Equal(fixture.WorkOrder.EquipmentId, first.EquipmentId);
    }

    [Fact]
    public void RepairPartConsumption_RequiresInProgressRepairAndAvailableStock()
    {
        var fixture = Fixture.Create();
        var scheduled = new MomServiceWorkOrder("MOM-SVC-SCHEDULED", MomServiceWorkOrderType.Repair,
            Guid.CreateVersion7(), "客户现场", "待开始维修", "admin");
        scheduled.Schedule(DateOnly.FromDateTime(DateTime.Today), "维修工程师");
        fixture.WorkOrderRepository.Add(scheduled);

        var stateError = Assert.Throws<InvalidOperationException>(() => fixture.Consumptions.Create(scheduled.Id,
            fixture.Product.Id, fixture.Warehouse.Id, fixture.Location.Id, 1m, "MOM-REP-002",
            DateOnly.FromDateTime(DateTime.Today), "维修工程师"));
        Assert.Contains("只有进行中的维修工单", stateError.Message);

        var stockError = Assert.Throws<InvalidOperationException>(() => fixture.Consumptions.Create(fixture.WorkOrder.Id,
            fixture.Product.Id, fixture.Warehouse.Id, fixture.Location.Id, 6m, "MOM-REP-003",
            DateOnly.FromDateTime(DateTime.Today), "维修工程师"));
        Assert.Contains("维修备件库存不足", stockError.Message);
        Assert.Empty(fixture.Consumptions.List(fixture.WorkOrder.Id));
        Assert.Single(fixture.Inventory.List());
    }

    [Fact]
    public void RepairPartConsumption_TransactionBoundaryRejectsBeforePersistence()
    {
        var fixture = Fixture.Create(new RejectingTransactionBoundary());

        Assert.Throws<InvalidOperationException>(() => fixture.Consumptions.Create(fixture.WorkOrder.Id,
            fixture.Product.Id, fixture.Warehouse.Id, fixture.Location.Id, 1m, "MOM-REP-004",
            DateOnly.FromDateTime(DateTime.Today), "维修工程师"));
        Assert.Empty(fixture.Consumptions.List(fixture.WorkOrder.Id));
        Assert.Single(fixture.Inventory.List());
    }

    private sealed class Fixture
    {
        public Product Product { get; private init; } = null!;
        public Warehouse Warehouse { get; private init; } = null!;
        public WarehouseLocation Location { get; private init; } = null!;
        public MomServiceWorkOrder WorkOrder { get; private init; } = null!;
        public WorkOrderRepository WorkOrderRepository { get; private init; } = null!;
        public PartConsumptionRepository PartRepository { get; private init; } = null!;
        public InventoryRepository Inventory { get; private init; } = null!;
        public InventoryService InventoryService { get; private init; } = null!;
        public MomServiceWorkOrderPartConsumptionService Consumptions { get; private init; } = null!;

        public static Fixture Create(IWorkflowTransactionBoundary? boundary = null)
        {
            var product = new Product("SP-001", "维修风扇", "件", null, null);
            var productRepository = new ProductRepository([product]);
            var warehouse = new Warehouse("WH-01", "维修备件仓", null);
            var location = warehouse.AddLocation("A-01", "维修备件库位");
            var warehouseRepository = new WarehouseRepository([warehouse]);
            var inventory = new InventoryRepository();
            var inventoryService = new InventoryService(inventory, productRepository, warehouseRepository);
            inventoryService.Create(product.Id, warehouse.Id, InventoryTransactionKind.Inbound, 5m, "SEED-REP-001",
                DateOnly.FromDateTime(DateTime.Today), "测试备件入库", location.Id, "B-001");

            var workOrder = new MomServiceWorkOrder("MOM-SVC-REPAIR-001", MomServiceWorkOrderType.Repair,
                Guid.CreateVersion7(), "客户现场", "设备维修", "admin");
            workOrder.Schedule(DateOnly.FromDateTime(DateTime.Today), "维修工程师");
            workOrder.Start("维修工程师");
            var workOrderRepository = new WorkOrderRepository([workOrder]);
            var workOrderService = new MomServiceWorkOrderService(workOrderRepository, new HistoryRepository(), null!);
            var partRepository = new PartConsumptionRepository();

            return new Fixture
            {
                Product = product, Warehouse = warehouse, Location = location, WorkOrder = workOrder,
                WorkOrderRepository = workOrderRepository, PartRepository = partRepository, Inventory = inventory,
                InventoryService = inventoryService,
                Consumptions = new MomServiceWorkOrderPartConsumptionService(partRepository, workOrderService,
                    inventoryService, new ProductService(productRepository), new WarehouseService(warehouseRepository), boundary)
            };
        }
    }

    private sealed class ProductRepository(IReadOnlyList<Product> seed) : IProductRepository
    {
        private readonly List<Product> items = seed.ToList();
        public IReadOnlyList<Product> List() => items;
        public void Add(Product item) => items.Add(item);
        public void Update(Product item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class WarehouseRepository(IReadOnlyList<Warehouse> seed) : IWarehouseRepository
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

    private sealed class InventoryRepository : IInventoryTransactionRepository
    {
        private readonly List<InventoryTransaction> items = [];
        public IReadOnlyList<InventoryTransaction> List() => items;
        public void Add(InventoryTransaction item) => items.Add(item);
    }

    private sealed class WorkOrderRepository(IReadOnlyList<MomServiceWorkOrder> seed) : IMomServiceWorkOrderRepository
    {
        private readonly List<MomServiceWorkOrder> items = seed.ToList();
        public IReadOnlyList<MomServiceWorkOrder> List(Guid? equipmentId = null) => equipmentId is Guid id ? items.Where(x => x.EquipmentId == id).ToArray() : items;
        public MomServiceWorkOrder? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public void Add(MomServiceWorkOrder item) => items.Add(item);
        public void Update(MomServiceWorkOrder item) { }
    }

    private sealed class PartConsumptionRepository : IMomServiceWorkOrderPartConsumptionRepository
    {
        private readonly List<MomServiceWorkOrderPartConsumption> items = [];
        public IReadOnlyList<MomServiceWorkOrderPartConsumption> List(Guid? serviceWorkOrderId = null) => serviceWorkOrderId is Guid id ? items.Where(x => x.ServiceWorkOrderId == id).ToArray() : items;
        public void Add(MomServiceWorkOrderPartConsumption item) => items.Add(item);
    }

    private sealed class HistoryRepository : IMomServiceWorkOrderHistoryRepository
    {
        public IReadOnlyList<MomServiceWorkOrderHistory> List(Guid workOrderId) => [];
        public void Add(MomServiceWorkOrderHistory item) { }
    }

    private sealed class RejectingTransactionBoundary : IWorkflowTransactionBoundary
    {
        public void Execute(Action operation, Action<Exception>? afterRollback = null) => throw new InvalidOperationException("模拟事务拒绝。 ");
    }
}
