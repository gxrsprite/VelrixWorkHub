using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.Warehouses;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class MomFinishedGoodsReceiptTests
{
    [Fact]
    public void CompletedWorkOrderReceiptWritesInventoryAndTracksRemainingQuantity()
    {
        var fixture = Fixture.Create();

        var first = fixture.Service.Create(fixture.WorkOrder.Id, fixture.Warehouse.Id, fixture.Location.Id, 6, "MOM-FGR-001", fixture.Today, batchNo: "FG-001");
        Assert.Equal(6m, first.Quantity);
        Assert.Equal(6m, fixture.Service.ReceivedQuantity(fixture.WorkOrder.Id));
        Assert.Single(fixture.Inventory.Items);
        Assert.Equal(InventoryTransactionKind.Inbound, fixture.Inventory.Items[0].Kind);
        Assert.Equal("MOM-FGR-001", fixture.Inventory.Items[0].SourceNo);

        fixture.Service.Create(fixture.WorkOrder.Id, fixture.Warehouse.Id, fixture.Location.Id, 4, "MOM-FGR-002", fixture.Today, batchNo: "FG-002");
        Assert.Equal(10m, fixture.Service.ReceivedQuantity(fixture.WorkOrder.Id));
        Assert.Equal(2, fixture.Inventory.Items.Count);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Create(fixture.WorkOrder.Id, fixture.Warehouse.Id, null, 1, "MOM-FGR-003", fixture.Today));
    }

    [Fact]
    public void ReceiptRequiresCompletedWorkOrderAndRejectsDuplicateSource()
    {
        var fixture = Fixture.Create(status: MomWorkOrderStatus.InProgress);

        Assert.Throws<InvalidOperationException>(() => fixture.Service.Create(fixture.WorkOrder.Id, fixture.Warehouse.Id, null, 1, "MOM-FGR-010", fixture.Today));
        var completed = Fixture.Create();
        completed.Service.Create(completed.WorkOrder.Id, completed.Warehouse.Id, null, 1, "MOM-FGR-010", completed.Today);

        Assert.Throws<InvalidOperationException>(() => completed.Service.Create(completed.WorkOrder.Id, completed.Warehouse.Id, null, 1, "mom-fgr-010", completed.Today));
        Assert.Single(completed.Inventory.Items);
    }

    [Fact]
    public void SerializedReceiptMustBeSingleAndInventoryFailureLeavesNoReceipt()
    {
        var fixture = Fixture.Create();
        Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Service.Create(fixture.WorkOrder.Id, fixture.Warehouse.Id, null, 2, "MOM-FGR-020", fixture.Today, serialNo: "SN-FG-001"));
        Assert.Throws<ArgumentException>(() => fixture.Service.Create(fixture.WorkOrder.Id, fixture.Warehouse.Id, null, 1, new string('X', 81), fixture.Today));

        var failing = Fixture.Create(inventoryFailure: new InvalidOperationException("模拟完工入库写入失败"));
        var error = Assert.Throws<InvalidOperationException>(() => failing.Service.Create(failing.WorkOrder.Id, failing.Warehouse.Id, null, 1, "MOM-FGR-021", failing.Today));
        Assert.Equal("模拟完工入库写入失败", error.Message);
        Assert.Empty(failing.Receipts.Items);
        Assert.Empty(failing.Inventory.Items);
    }

    private sealed class Fixture
    {
        public DateOnly Today { get; } = new(2026, 8, 7);
        public Product Product { get; private set; } = null!;
        public Warehouse Warehouse { get; private set; } = null!;
        public WarehouseLocation Location { get; private set; } = null!;
        public MomWorkOrder WorkOrder { get; private set; } = null!;
        public InMemoryReceiptRepository Receipts { get; private set; } = null!;
        public InMemoryInventoryRepository Inventory { get; private set; } = null!;
        public MomFinishedGoodsReceiptService Service { get; private set; } = null!;

        public static Fixture Create(MomWorkOrderStatus status = MomWorkOrderStatus.Completed, InvalidOperationException? inventoryFailure = null)
        {
            var fixture = new Fixture();
            var product = new Product("FG-RECEIPT-001", "完工成品", "件", 100, null);
            var warehouse = new Warehouse("WH-FG-001", "成品仓", null);
            var location = warehouse.AddLocation("FG-A01", "成品库位");
            var workOrder = MomWorkOrder.Restore(Guid.CreateVersion7(), "MO-FGR-001", product.Id, fixture.Today, fixture.Today.AddDays(3),
                10, 10, status, MomWorkOrderSourceKind.Manual, null, null, null, null, "{}");
            var receipts = new InMemoryReceiptRepository();
            var inventory = new InMemoryInventoryRepository(inventoryFailure);
            var products = new InMemoryProductRepository(product);
            var warehouses = new InMemoryWarehouseRepository(warehouse);
            var inventoryService = new InventoryService(inventory, products, warehouses);
            var workOrders = new InMemoryWorkOrderRepository(workOrder);
            fixture.Product = product; fixture.Warehouse = warehouse; fixture.Location = location; fixture.WorkOrder = workOrder;
            fixture.Receipts = receipts; fixture.Inventory = inventory;
            fixture.Service = new MomFinishedGoodsReceiptService(receipts, workOrders, inventory, inventoryService);
            return fixture;
        }
    }

    private sealed class InMemoryReceiptRepository : IMomFinishedGoodsReceiptRepository
    {
        public List<MomFinishedGoodsReceipt> Items { get; } = [];
        public IReadOnlyList<MomFinishedGoodsReceipt> List() => Items;
        public void Add(MomFinishedGoodsReceipt item) => Items.Add(item);
    }

    private sealed class InMemoryWorkOrderRepository(params MomWorkOrder[] seed) : IMomWorkOrderRepository
    {
        private readonly List<MomWorkOrder> items = [.. seed];
        public IReadOnlyList<MomWorkOrder> List() => items;
        public void Add(MomWorkOrder item) => items.Add(item);
        public void Update(MomWorkOrder item) { }
    }

    private sealed class InMemoryInventoryRepository(InvalidOperationException? failure = null) : IInventoryTransactionRepository
    {
        public List<InventoryTransaction> Items { get; } = [];
        public IReadOnlyList<InventoryTransaction> List() => Items;
        public void Add(InventoryTransaction item) { if (failure is not null) throw failure; Items.Add(item); }
    }

    private sealed class InMemoryProductRepository(params Product[] seed) : IProductRepository
    {
        private readonly List<Product> items = [.. seed];
        public IReadOnlyList<Product> List() => items;
        public void Add(Product item) => items.Add(item);
        public void Update(Product item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class InMemoryWarehouseRepository(params Warehouse[] seed) : IWarehouseRepository
    {
        private readonly List<Warehouse> items = [.. seed];
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
