using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.SalesOrders;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Application.Warehouses;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class MomFinishedGoodsShipmentTests
{
    [Fact]
    public void ShipmentWritesOutboundTraceAndConfirmsSalesOrder()
    {
        var fixture = Fixture.Create();

        var item = fixture.Service.Create(fixture.Order.Id, fixture.Receipt.Id, fixture.Today);

        Assert.Equal(SalesOrderStatus.Shipped, fixture.Order.Status);
        Assert.Equal(fixture.Order.Quantity, item.Quantity);
        Assert.Equal(InventoryTransactionKind.Outbound, Assert.Single(fixture.Inventory.Items, x => x.Kind == InventoryTransactionKind.Outbound).Kind);
        Assert.Equal($"{fixture.Order.OrderNo}-OUT", item.SourceNo);
        Assert.Same(item, Assert.Single(fixture.Shipments.Items));
    }

    [Fact]
    public void ShipmentRequiresSubmittedMatchingOrderAndEnoughReceivedGoods()
    {
        var fixture = Fixture.Create(orderQuantity: 5m, receiptQuantity: 4m);

        var error = Assert.Throws<InvalidOperationException>(() => fixture.Service.Create(fixture.Order.Id, fixture.Receipt.Id, fixture.Today));
        Assert.Contains("可用数量不足", error.Message);
        Assert.Equal(SalesOrderStatus.Submitted, fixture.Order.Status);
        Assert.Empty(fixture.Shipments.Items);

        var mismatch = Fixture.Create(receiptProduct: new Product("OTHER-001", "其他成品", "件", 1, null));
        Assert.Throws<InvalidOperationException>(() => mismatch.Service.Create(mismatch.Order.Id, mismatch.Receipt.Id, mismatch.Today));
        Assert.Equal(SalesOrderStatus.Submitted, mismatch.Order.Status);
    }

    [Fact]
    public void PartialShipmentsCanUseMultipleReceiptsAndCompleteOnlyOnTheFinalLot()
    {
        var fixture = Fixture.Create(orderQuantity: 5m, receiptQuantity: 3m);
        var secondReceipt = fixture.AddReceipt(2m, "MOM-FGR-SHIP-002", "FG-SHIP-002");

        var first = fixture.Service.Create(fixture.Order.Id, fixture.Receipt.Id, fixture.Today, 2m);
        var second = fixture.Service.Create(fixture.Order.Id, secondReceipt.Id, fixture.Today, 2m);

        Assert.Equal(SalesOrderStatus.Submitted, fixture.Order.Status);
        Assert.Equal(1m, fixture.Service.RemainingQuantity(fixture.Order.Id));
        Assert.Equal("SO-MOM-08B-001-OUT", first.SourceNo);
        Assert.Equal("SO-MOM-08B-001-OUT-P02", second.SourceNo);

        var final = fixture.Service.Create(fixture.Order.Id, fixture.Receipt.Id, fixture.Today, 1m);

        Assert.Equal(SalesOrderStatus.Shipped, fixture.Order.Status);
        Assert.Equal(0m, fixture.Service.RemainingQuantity(fixture.Order.Id));
        Assert.Equal("SO-MOM-08B-001-OUT-P03", final.SourceNo);
        Assert.Equal(3, fixture.Shipments.Items.Count);
        Assert.Equal(5m, fixture.Service.List(fixture.Order.Id).Sum(x => x.Quantity));
        Assert.Equal(3, fixture.Inventory.Items.Count(x => x.Kind == InventoryTransactionKind.Outbound));
    }

    [Fact]
    public void PartialShipmentCannotExceedOrderOrReceiptRemainingQuantity()
    {
        var fixture = Fixture.Create(orderQuantity: 5m, receiptQuantity: 5m);

        fixture.Service.Create(fixture.Order.Id, fixture.Receipt.Id, fixture.Today, 4m);

        var error = Assert.Throws<InvalidOperationException>(() => fixture.Service.Create(fixture.Order.Id, fixture.Receipt.Id, fixture.Today, 2m));

        Assert.Contains("销售订单剩余数量", error.Message);
        Assert.Equal(SalesOrderStatus.Submitted, fixture.Order.Status);
        Assert.Single(fixture.Shipments.Items);
        Assert.Equal(1m, fixture.Service.ReceiptRemainingQuantity(fixture.Receipt.Id));
    }

    [Fact]
    public void MultiSourceShipmentAllocatesEachReceiptAndCompletesOrderAtomically()
    {
        var fixture = Fixture.Create(orderQuantity: 5m, receiptQuantity: 3m);
        var secondReceipt = fixture.AddReceipt(2m, "MOM-FGR-SHIP-002", "FG-SHIP-002");

        var shipment = fixture.Service.CreateFromReceipts(fixture.Order.Id, fixture.Today,
        [
            new MomFinishedGoodsShipmentAllocationRequest(fixture.Receipt.Id, 3m),
            new MomFinishedGoodsShipmentAllocationRequest(secondReceipt.Id, 2m)
        ]);

        Assert.Equal(SalesOrderStatus.Shipped, fixture.Order.Status);
        Assert.Equal(5m, shipment.Quantity);
        Assert.Equal("SO-MOM-08B-001-OUT", shipment.SourceNo);
        var allocations = fixture.Allocations.Items.OrderBy(x => x.SourceNo).ToArray();
        Assert.Equal(2, allocations.Length);
        Assert.All(allocations, x => Assert.Equal(shipment.Id, x.ShipmentId));
        Assert.Equal(["SO-MOM-08B-001-OUT-A01", "SO-MOM-08B-001-OUT-A02"], allocations.Select(x => x.SourceNo).ToArray());
        Assert.Equal(3m, allocations[0].Quantity);
        Assert.Equal(2m, allocations[1].Quantity);
        Assert.Equal(0m, fixture.Service.ReceiptRemainingQuantity(fixture.Receipt.Id));
        Assert.Equal(0m, fixture.Service.ReceiptRemainingQuantity(secondReceipt.Id));
        Assert.Equal(5m, fixture.Service.ShippedQuantity(fixture.Order.Id));
        Assert.Equal(2, fixture.Inventory.Items.Count(x => x.Kind == InventoryTransactionKind.Outbound));
    }

    [Fact]
    public void MultiSourceShipmentRejectsAggregateOverflowBeforeWritingAnything()
    {
        var fixture = Fixture.Create(orderQuantity: 5m, receiptQuantity: 5m);
        var secondReceipt = fixture.AddReceipt(5m, "MOM-FGR-SHIP-002", "FG-SHIP-002");

        var error = Assert.Throws<InvalidOperationException>(() => fixture.Service.CreateFromReceipts(fixture.Order.Id, fixture.Today,
        [
            new MomFinishedGoodsShipmentAllocationRequest(fixture.Receipt.Id, 3m),
            new MomFinishedGoodsShipmentAllocationRequest(secondReceipt.Id, 3m)
        ]));

        Assert.Contains("销售订单剩余数量", error.Message);
        Assert.Equal(SalesOrderStatus.Submitted, fixture.Order.Status);
        Assert.Empty(fixture.Shipments.Items);
        Assert.Empty(fixture.Allocations.Items);
        Assert.Equal(2, fixture.Inventory.Items.Count); // both pre-existing receipt inbound transactions remain
    }

    [Fact]
    public void MultiSourceShipmentRollsBackEarlierOutboundWhenALaterSourceFails()
    {
        var fixture = Fixture.Create(orderQuantity: 5m, receiptQuantity: 3m,
            inventoryFailure: new InvalidOperationException("模拟第二来源出库失败"), failOutboundAt: 2);
        var secondReceipt = fixture.AddReceipt(2m, "MOM-FGR-SHIP-002", "FG-SHIP-002");
        fixture.Service = new MomFinishedGoodsShipmentService(fixture.Shipments, fixture.Allocations, fixture.Receipts,
            new InMemorySalesOrderRepository(fixture.Order), new ShipmentStatusWriter(), fixture.Inventory, fixture.InventoryService,
            new SnapshotRollbackBoundary(fixture.Order, fixture.Inventory, fixture.Shipments, fixture.Allocations));

        var error = Assert.Throws<InvalidOperationException>(() => fixture.Service.CreateFromReceipts(fixture.Order.Id, fixture.Today,
        [
            new MomFinishedGoodsShipmentAllocationRequest(fixture.Receipt.Id, 3m),
            new MomFinishedGoodsShipmentAllocationRequest(secondReceipt.Id, 2m)
        ]));

        Assert.Equal("模拟第二来源出库失败", error.Message);
        Assert.Equal(SalesOrderStatus.Submitted, fixture.Order.Status);
        Assert.Empty(fixture.Shipments.Items);
        Assert.Empty(fixture.Allocations.Items);
        Assert.Equal(2, fixture.Inventory.Items.Count); // only the two receipt inbound transactions remain
        Assert.DoesNotContain(fixture.Inventory.Items, x => x.Kind == InventoryTransactionKind.Outbound);
    }

    [Fact]
    public void ShipmentRollsBackOrderStatusWhenOutboundWriteFails()
    {
        var fixture = Fixture.Create(inventoryFailure: new InvalidOperationException("模拟发运出库失败"), transactions: new RollbackBoundary());

        var error = Assert.Throws<InvalidOperationException>(() => fixture.Service.Create(fixture.Order.Id, fixture.Receipt.Id, fixture.Today));

        Assert.Equal("模拟发运出库失败", error.Message);
        Assert.Equal(SalesOrderStatus.Submitted, fixture.Order.Status);
        Assert.Empty(fixture.Shipments.Items);
        Assert.Single(fixture.Inventory.Items); // only the pre-existing receipt inbound transaction remains
    }

    private sealed class Fixture
    {
        public DateOnly Today { get; } = new(2026, 8, 7);
        public Product Product { get; private set; } = null!;
        public Warehouse Warehouse { get; private set; } = null!;
        public SalesOrder Order { get; private set; } = null!;
        public MomFinishedGoodsReceipt Receipt { get; private set; } = null!;
        public InMemoryReceiptRepository Receipts { get; private set; } = null!;
        public InMemoryInventoryRepository Inventory { get; private set; } = null!;
        public InventoryService InventoryService { get; private set; } = null!;
        public InMemoryShipmentRepository Shipments { get; private set; } = null!;
        public InMemoryShipmentAllocationRepository Allocations { get; private set; } = null!;
        public MomFinishedGoodsShipmentService Service { get; set; } = null!;

        public static Fixture Create(decimal orderQuantity = 5m, decimal receiptQuantity = 5m, Product? receiptProduct = null,
            InvalidOperationException? inventoryFailure = null, int? failOutboundAt = null, IWorkflowTransactionBoundary? transactions = null)
        {
            var fixture = new Fixture();
            var product = new Product("FG-SHIP-001", "发运成品", "件", 100, null);
            var receiptProductValue = receiptProduct ?? product;
            var warehouse = new Warehouse("WH-SHIP-001", "成品仓", null);
            var location = warehouse.AddLocation("FG-A01", "成品库位");
            var customerId = Guid.CreateVersion7();
            var order = new SalesOrder("SO-MOM-08B-001", customerId, product.Id, fixture.Today, orderQuantity, 120);
            order.SetStatus(SalesOrderStatus.Submitted);
            var receipt = new MomFinishedGoodsReceipt(Guid.CreateVersion7(), receiptProductValue.Id, warehouse.Id, location.Id, receiptQuantity,
                "MOM-FGR-SHIP-001", fixture.Today, batchNo: "FG-SHIP-001");
            var inventory = new InMemoryInventoryRepository(inventoryFailure, failOutboundAt);
            var products = new InMemoryProductRepository(product, receiptProductValue);
            var warehouses = new InMemoryWarehouseRepository(warehouse);
            var inventoryService = new InventoryService(inventory, products, warehouses);
            inventoryService.Create(receipt.ProductId, warehouse.Id, InventoryTransactionKind.Inbound, receiptQuantity, receipt.SourceNo,
                fixture.Today, "完工入库", location.Id, receipt.BatchNo, receipt.ExpiryDate, receipt.SerialNo);
            var orders = new InMemorySalesOrderRepository(order);
            var receipts = new InMemoryReceiptRepository(receipt);
            var shipments = new InMemoryShipmentRepository();
            var allocations = new InMemoryShipmentAllocationRepository();
            fixture.Product = product; fixture.Warehouse = warehouse; fixture.Order = order; fixture.Receipt = receipt;
            fixture.Receipts = receipts; fixture.Inventory = inventory; fixture.InventoryService = inventoryService; fixture.Shipments = shipments; fixture.Allocations = allocations;
            fixture.Service = new MomFinishedGoodsShipmentService(shipments, allocations, receipts, orders, new ShipmentStatusWriter(), inventory, inventoryService, transactions);
            return fixture;
        }

        public MomFinishedGoodsReceipt AddReceipt(decimal quantity, string sourceNo, string batchNo)
        {
            var location = Warehouse.Locations.Single();
            var receipt = new MomFinishedGoodsReceipt(Guid.CreateVersion7(), Product.Id, Warehouse.Id, location.Id, quantity,
                sourceNo, Today, batchNo: batchNo);
            Receipts.Add(receipt);
            InventoryService.Create(receipt.ProductId, receipt.WarehouseId, InventoryTransactionKind.Inbound, quantity, sourceNo,
                Today, "完工入库", receipt.LocationId, receipt.BatchNo, receipt.ExpiryDate, receipt.SerialNo);
            return receipt;
        }
    }

    private sealed class ShipmentStatusWriter : ISalesOrderShipmentService
    {
        public void ConfirmShipped(SalesOrder item) { item.SetStatus(SalesOrderStatus.Shipped); }
        public void RestoreSubmittedAfterRollback(SalesOrder item) { if (item.Status == SalesOrderStatus.Shipped) item.SetStatusForRecovery(SalesOrderStatus.Submitted); }
    }

    private sealed class InMemoryShipmentRepository(params MomFinishedGoodsShipment[] seed) : IMomFinishedGoodsShipmentRepository
    {
        public List<MomFinishedGoodsShipment> Items { get; } = [.. seed];
        public IReadOnlyList<MomFinishedGoodsShipment> List() => Items;
        public void Add(MomFinishedGoodsShipment item) => Items.Add(item);
    }

    private sealed class InMemoryShipmentAllocationRepository(params MomFinishedGoodsShipmentAllocation[] seed) : IMomFinishedGoodsShipmentAllocationRepository
    {
        public List<MomFinishedGoodsShipmentAllocation> Items { get; } = [.. seed];
        public IReadOnlyList<MomFinishedGoodsShipmentAllocation> List(Guid? shipmentId = null) => shipmentId is Guid selected
            ? Items.Where(x => x.ShipmentId == selected).ToArray()
            : Items;
        public void Add(MomFinishedGoodsShipmentAllocation item) => Items.Add(item);
    }

    private sealed class InMemoryReceiptRepository(params MomFinishedGoodsReceipt[] seed) : IMomFinishedGoodsReceiptRepository
    {
        private readonly List<MomFinishedGoodsReceipt> items = [.. seed];
        public IReadOnlyList<MomFinishedGoodsReceipt> List() => items;
        public void Add(MomFinishedGoodsReceipt item) => items.Add(item);
    }

    private sealed class InMemorySalesOrderRepository(params SalesOrder[] seed) : ISalesOrderRepository
    {
        private readonly List<SalesOrder> items = [.. seed];
        public IReadOnlyList<SalesOrder> List() => items;
        public void Add(SalesOrder item) => items.Add(item);
        public void Update(SalesOrder item) { }
    }

    private sealed class InMemoryInventoryRepository(InvalidOperationException? failure = null, int? failOutboundAt = null) : IInventoryTransactionRepository
    {
        private int outboundWrites;
        public List<InventoryTransaction> Items { get; } = [];
        public IReadOnlyList<InventoryTransaction> List() => Items;
        public void Add(InventoryTransaction item)
        {
            if (item.Kind == InventoryTransactionKind.Outbound)
            {
                outboundWrites++;
                if (failure is not null && (failOutboundAt is null || outboundWrites == failOutboundAt)) throw failure;
            }
            Items.Add(item);
        }
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

    private sealed class RollbackBoundary : IWorkflowTransactionBoundary
    {
        public void Execute(Action operation, Action<Exception>? afterRollback = null)
        {
            try { operation(); } catch (Exception exception) { afterRollback?.Invoke(exception); throw; }
        }
    }

    private sealed class SnapshotRollbackBoundary(SalesOrder order, InMemoryInventoryRepository inventory,
        InMemoryShipmentRepository shipments, InMemoryShipmentAllocationRepository allocations) : IWorkflowTransactionBoundary
    {
        public void Execute(Action operation, Action<Exception>? afterRollback = null)
        {
            var inventoryCount = inventory.Items.Count;
            var shipmentCount = shipments.Items.Count;
            var allocationCount = allocations.Items.Count;
            try { operation(); }
            catch (Exception exception)
            {
                inventory.Items.RemoveRange(inventoryCount, inventory.Items.Count - inventoryCount);
                shipments.Items.RemoveRange(shipmentCount, shipments.Items.Count - shipmentCount);
                allocations.Items.RemoveRange(allocationCount, allocations.Items.Count - allocationCount);
                afterRollback?.Invoke(exception);
                if (order.Status == SalesOrderStatus.Shipped) order.SetStatusForRecovery(SalesOrderStatus.Submitted);
                throw;
            }
        }
    }
}
