using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Application.PurchaseOrders;
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
    }

    private sealed class SettlementRepository : ISettlementRepository
    {
        public IReadOnlyList<ErpSettlement> List() => [];
        public void Add(ErpSettlement item) { }
        public void Update(ErpSettlement item) { }
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
