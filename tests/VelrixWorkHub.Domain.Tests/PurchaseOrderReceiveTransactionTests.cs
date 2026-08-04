using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Application.PurchaseOrders;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.Settlements;
using VelrixWorkHub.Application.Warehouses;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PurchaseOrderReceiveTransactionTests
{
    [Fact]
    public void ReceiveAtomicallyUpdatesOrderAndCreatesInboundTransaction()
    {
        var order = CreateSubmittedOrder("PO-RECEIVE-ATOMIC");
        var inventory = new InventoryRepository();
        var service = CreateService(order, inventory);

        service.Receive(order);

        Assert.Equal(PurchaseOrderStatus.Received, order.Status);
        var transaction = Assert.Single(inventory.Items);
        Assert.Equal(order.ProductId, transaction.ProductId);
        Assert.Equal(8m, transaction.Quantity);
        Assert.Equal("PO-RECEIVE-ATOMIC-IN", transaction.SourceNo);
        Assert.Equal(InventoryTransactionKind.Inbound, transaction.Kind);

        Assert.Throws<InvalidOperationException>(() => service.Receive(order));
        Assert.Single(inventory.Items);
    }

    [Fact]
    public void ReceiveRollsBackOrderStatusWhenInboundTransactionFails()
    {
        var order = CreateSubmittedOrder("PO-RECEIVE-ROLLBACK");
        var inventory = new InventoryRepository { Failure = new InvalidOperationException("模拟入库写入失败") };
        var service = CreateService(order, inventory, new RollbackTransactionBoundary());

        var error = Assert.Throws<InvalidOperationException>(() => service.Receive(order));

        Assert.Equal("模拟入库写入失败", error.Message);
        Assert.Equal(PurchaseOrderStatus.Submitted, order.Status);
        Assert.Empty(inventory.Items);
    }

    [Fact]
    public void ReceiveRejectsExistingInboundSourceBeforeChangingOrder()
    {
        var order = CreateSubmittedOrder("PO-RECEIVE-DUPLICATE");
        var inventory = new InventoryRepository();
        inventory.Items.Add(new InventoryTransaction(order.ProductId, Guid.CreateVersion7(), InventoryTransactionKind.Inbound, order.Quantity, "PO-RECEIVE-DUPLICATE-IN", order.OrderDate, "已有入库流水"));
        var service = CreateService(order, inventory);

        var error = Assert.Throws<InvalidOperationException>(() => service.Receive(order));

        Assert.Contains("已生成入库流水", error.Message);
        Assert.Equal(PurchaseOrderStatus.Submitted, order.Status);
        Assert.Single(inventory.Items);
    }

    [Fact]
    public void Receive_UsesInventoryServiceAndRollsBackWhenProductWasDisabledAfterApproval()
    {
        var product = new Product("SKU-RECEIVE-CAP", "收货容量商品", "件", 12.5m, null);
        var order = new PurchaseOrder("PO-RECEIVE-CAPACITY", Guid.CreateVersion7(), product.Id, new DateOnly(2026, 7, 22), 8m, 12.5m);
        order.SetStatus(PurchaseOrderStatus.Submitted);
        var inventory = new InventoryRepository();
        var warehouse = new Warehouse("WH-RECEIVE-CAP", "收货容量仓", null);
        var warehouses = new WarehouseRepository(warehouse);
        var inventoryService = new InventoryService(inventory, new ProductRepository(product), warehouses);
        var service = new PurchaseOrderService(new PurchaseOrderRepository(order), null!, null!, inventory, warehouses, new SettlementRepository(), transactions: new RollbackTransactionBoundary(), inventoryService: inventoryService);
        product.SetActive(false);

        var error = Assert.Throws<InvalidOperationException>(() => service.Receive(order));

        Assert.Contains("商品已停用", error.Message);
        Assert.Equal(PurchaseOrderStatus.Submitted, order.Status);
        Assert.Empty(inventory.Items);
    }

    [Fact]
    public void Receive_UsesInventoryServiceAndRollsBackWhenWarehouseWasDisabledAfterApproval()
    {
        var product = new Product("SKU-RECEIVE-WH", "收货仓库门禁商品", "件", 12.5m, null);
        var order = new PurchaseOrder("PO-RECEIVE-WAREHOUSE", Guid.CreateVersion7(), product.Id, new DateOnly(2026, 7, 22), 8m, 12.5m);
        order.SetStatus(PurchaseOrderStatus.Submitted);
        var inventory = new InventoryRepository();
        var warehouse = new Warehouse("WH-RECEIVE-DISABLED", "已停用收货仓", null);
        warehouse.SetActive(false);
        var warehouses = new WarehouseRepository(warehouse);
        var inventoryService = new InventoryService(inventory, new ProductRepository(product), warehouses);
        var service = new PurchaseOrderService(new PurchaseOrderRepository(order), null!, null!, inventory, warehouses, new SettlementRepository(), transactions: new RollbackTransactionBoundary(), inventoryService: inventoryService);

        var error = Assert.Throws<InvalidOperationException>(() => service.Receive(order));

        Assert.Contains("没有可用的启用仓库", error.Message);
        Assert.Equal(PurchaseOrderStatus.Submitted, order.Status);
        Assert.Empty(inventory.Items);
    }

    [Fact]
    public void Receive_ToSelectedLocationRejectsProductCapacityWithoutCreatingInboundTransaction()
    {
        var product = new Product("SKU-RECEIVE-LOCATION-CAP", "收货库位容量商品", "件", 12.5m, null);
        var order = new PurchaseOrder("PO-RECEIVE-LOCATION-CAP", Guid.CreateVersion7(), product.Id, new DateOnly(2026, 7, 22), 8m, 12.5m);
        order.SetStatus(PurchaseOrderStatus.Submitted);
        var inventory = new InventoryRepository();
        var warehouse = new Warehouse("WH-RECEIVE-LOCATION-CAP", "收货库位容量仓", null);
        var location = warehouse.AddLocation("A-01", "收货库位");
        location.SetProductCapacity(product.Id, 5m);
        var warehouses = new WarehouseRepository(warehouse);
        var inventoryService = new InventoryService(inventory, new ProductRepository(product), warehouses);
        var service = new PurchaseOrderService(new PurchaseOrderRepository(order), null!, null!, inventory, warehouses, new SettlementRepository(), transactions: new RollbackTransactionBoundary(), inventoryService: inventoryService);

        var error = Assert.Throws<InvalidOperationException>(() => service.Receive(order, warehouse.Id, location.Id));

        Assert.Contains("容量", error.Message);
        Assert.Equal(PurchaseOrderStatus.Submitted, order.Status);
        Assert.Empty(inventory.Items);
    }

    private static PurchaseOrder CreateSubmittedOrder(string orderNo)
    {
        var item = new PurchaseOrder(orderNo, Guid.CreateVersion7(), Guid.CreateVersion7(), new DateOnly(2026, 7, 22), 8m, 12.5m);
        item.SetStatus(PurchaseOrderStatus.Submitted);
        return item;
    }

    private static PurchaseOrderService CreateService(PurchaseOrder order, InventoryRepository inventory, IWorkflowTransactionBoundary? transactions = null)
    {
        return new PurchaseOrderService(
            new PurchaseOrderRepository(order),
            null!,
            null!,
            inventory,
            new WarehouseRepository(new Warehouse("WH-RECEIVE", "收货仓", null)),
            new SettlementRepository(),
            transactions: transactions);
    }

    private sealed class PurchaseOrderRepository(params PurchaseOrder[] seed) : IPurchaseOrderRepository
    {
        private readonly List<PurchaseOrder> items = [.. seed];
        public IReadOnlyList<PurchaseOrder> List() => items;
        public void Add(PurchaseOrder item) => items.Add(item);
        public void Update(PurchaseOrder item) { }
    }

    private sealed class InventoryRepository : IInventoryTransactionRepository
    {
        public List<InventoryTransaction> Items { get; } = [];
        public InvalidOperationException? Failure { get; init; }
        public IReadOnlyList<InventoryTransaction> List() => Items;
        public void Add(InventoryTransaction item)
        {
            if (Failure is not null) throw Failure;
            Items.Add(item);
        }
    }

    private sealed class WarehouseRepository(params Warehouse[] seed) : IWarehouseRepository
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

    private sealed class SettlementRepository : ISettlementRepository
    {
        public IReadOnlyList<ErpSettlement> List() => [];
        public void Add(ErpSettlement item) { }
        public void Update(ErpSettlement item) { }
    }

    private sealed class ProductRepository(params Product[] seed) : IProductRepository
    {
        private readonly List<Product> items = [.. seed];
        public IReadOnlyList<Product> List() => items;
        public void Add(Product item) => items.Add(item);
        public void Update(Product item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class RollbackTransactionBoundary : IWorkflowTransactionBoundary
    {
        public void Execute(Action operation, Action<Exception>? afterRollback = null)
        {
            try { operation(); }
            catch (Exception exception) { afterRollback?.Invoke(exception); throw; }
        }
    }
}
