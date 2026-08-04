using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.Warehouses;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class InventoryServiceTests
{
    [Fact]
    public void Outbound_RejectsQuantityAboveBalance()
    {
        var fixture = new InventoryFixture(5m);

        Assert.Throws<InvalidOperationException>(() => fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Outbound, 6, "OUT-1", DateOnly.FromDateTime(DateTime.Today), null));
    }

    [Fact]
    public void Transfer_CreatesPairedTransactionsAndKeepsTotalBalance()
    {
        var fixture = new InventoryFixture(5m);
        var target = new Warehouse("WH-002", "华南仓", null);
        fixture.Warehouses.Items.Add(target);

        fixture.Service.Transfer(fixture.Product.Id, fixture.Warehouse.Id, null, target.Id, null, 2, "TR-1", DateOnly.FromDateTime(DateTime.Today));

        Assert.Equal(2, fixture.Transactions.Items.Count(x => x.SourceNo.StartsWith("TR-1", StringComparison.Ordinal)));
        Assert.Equal(5m, fixture.Service.Balances().Sum(x => x.Quantity));
    }

    [Fact]
    public void Transfer_UsesConfiguredTransactionBoundaryForPairedInventoryWrites()
    {
        var boundary = new RecordingTransactionBoundary();
        var fixture = new InventoryFixture(5m, boundary);
        var target = new Warehouse("WH-002", "华南仓", null);
        fixture.Warehouses.Items.Add(target);

        fixture.Service.Transfer(fixture.Product.Id, fixture.Warehouse.Id, null, target.Id, null, 2m, "TR-TX", DateOnly.FromDateTime(DateTime.Today));

        Assert.Equal(1, boundary.ExecuteCount);
        Assert.Equal(2, fixture.Transactions.Items.Count(x => x.SourceNo.StartsWith("TR-TX", StringComparison.Ordinal)));
    }

    [Fact]
    public void Transfer_RejectsInactiveTargetWithoutPartialOutbound()
    {
        var fixture = new InventoryFixture(5m);
        var target = new Warehouse("WH-002", "停用仓", null);
        target.SetActive(false);
        fixture.Warehouses.Items.Add(target);

        Assert.Throws<InvalidOperationException>(() => fixture.Service.Transfer(fixture.Product.Id, fixture.Warehouse.Id, null, target.Id, null, 2, "TR-INACTIVE", DateOnly.FromDateTime(DateTime.Today)));

        Assert.DoesNotContain(fixture.Transactions.Items, x => x.SourceNo.StartsWith("TR-INACTIVE", StringComparison.Ordinal));
        Assert.Equal(5m, fixture.Service.Balances().Single(x => x.WarehouseId == fixture.Warehouse.Id).Quantity);
    }

    [Fact]
    public void Stocktake_CreatesOnlyTheDifferenceAdjustment()
    {
        var fixture = new InventoryFixture(5m);

        var item = fixture.Service.Stocktake(fixture.Product.Id, fixture.Warehouse.Id, 7, "ST-1", DateOnly.FromDateTime(DateTime.Today));

        Assert.Equal(2m, item.SignedQuantity);
        Assert.Equal(7m, fixture.Service.Balances().Single().Quantity);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Stocktake(fixture.Product.Id, fixture.Warehouse.Id, 7, "ST-2", item.OccurredOn));
    }

    [Fact]
    public void Create_RejectsLocationFromAnotherWarehouse()
    {
        var fixture = new InventoryFixture(5m);
        var other = new Warehouse("WH-002", "华南仓", null);
        var otherLocation = other.AddLocation("B-01", "南区货架");
        fixture.Warehouses.Items.Add(other);

        Assert.Throws<InvalidOperationException>(() => fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 1, "IN-LOCATION", DateOnly.FromDateTime(DateTime.Today), null, otherLocation.Id));
    }

    [Fact]
    public void LocationBalances_SeparatesSpecifiedAndUnspecifiedStock()
    {
        var fixture = new InventoryFixture(5m);
        var location = fixture.Warehouse.Locations.Single();
        fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 2, "IN-LOCATION", DateOnly.FromDateTime(DateTime.Today), null, location.Id);

        var balances = fixture.Service.LocationBalances();

        Assert.Contains(balances, x => x.LocationId is null && x.Quantity == 5m);
        Assert.Contains(balances, x => x.LocationId == location.Id && x.Quantity == 2m);
    }

    [Fact]
    public void Outbound_FromSpecifiedLocation_UsesLocationBalance()
    {
        var fixture = new InventoryFixture(5m);
        var location = fixture.Warehouse.Locations.Single();

        Assert.Throws<InvalidOperationException>(() => fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Outbound, 1, "OUT-LOCATION", DateOnly.FromDateTime(DateTime.Today), null, location.Id));
    }

    [Fact]
    public void Create_RejectsInactiveProductAndWarehouse()
    {
        var fixture = new InventoryFixture(5m);
        fixture.Product.SetActive(false);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 1, "IN-INACTIVE-PRODUCT", DateOnly.FromDateTime(DateTime.Today), null));

        fixture.Product.SetActive(true);
        fixture.Warehouse.SetActive(false);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 1, "IN-INACTIVE-WAREHOUSE", DateOnly.FromDateTime(DateTime.Today), null));
    }

    [Fact]
    public void Stocktake_FromSpecifiedLocation_UsesLocationBookQuantity()
    {
        var fixture = new InventoryFixture(5m);
        var location = fixture.Warehouse.Locations.Single();
        fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 2, "LOCATION-OPENING", DateOnly.FromDateTime(DateTime.Today), null, location.Id);

        var adjustment = fixture.Service.Stocktake(fixture.Product.Id, fixture.Warehouse.Id, 3, "ST-LOCATION", DateOnly.FromDateTime(DateTime.Today), location.Id);

        Assert.Equal(1m, adjustment.SignedQuantity);
        Assert.Equal(location.Id, adjustment.LocationId);
        Assert.Equal(3m, fixture.Service.LocationBalances().Single(x => x.LocationId == location.Id).Quantity);
    }

    [Fact]
    public void BatchOutboundTransferAndStocktakePreserveBatchTraceability()
    {
        var fixture = new InventoryFixture(1m);
        var batchDate = DateOnly.FromDateTime(DateTime.Today);
        var expiry = batchDate.AddYears(1);
        fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 5m, "BATCH-OPENING", batchDate, null, batchNo: "LOT-A", expiryDate: expiry);
        var target = new Warehouse("WH-002", "华南仓", null);
        fixture.Warehouses.Items.Add(target);

        Assert.Throws<InvalidOperationException>(() => fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Outbound, 6m, "BATCH-OVER", batchDate, null, batchNo: "LOT-A", expiryDate: expiry));
        fixture.Service.Transfer(fixture.Product.Id, fixture.Warehouse.Id, null, target.Id, null, 2m, "TR-BATCH", batchDate, "LOT-A", expiry);
        var adjustment = fixture.Service.Stocktake(fixture.Product.Id, target.Id, 3m, "ST-BATCH", batchDate, null, "LOT-A", expiry);

        Assert.Equal(1m, adjustment.SignedQuantity);
        Assert.All(fixture.Transactions.Items.Where(x => x.SourceNo.StartsWith("TR-BATCH", StringComparison.Ordinal)), x => { Assert.Equal("LOT-A", x.BatchNo); Assert.Equal(expiry, x.ExpiryDate); });
        Assert.Contains(fixture.Service.BatchBalances(), x => x.WarehouseId == target.Id && x.BatchNo == "LOT-A" && x.Quantity == 3m);
    }

    [Fact]
    public void ExpiryAlerts_ReturnOnlyPositiveBatchBalancesThatAreExpiredOrNearExpiry()
    {
        var fixture = new InventoryFixture(1m);
        var today = new DateOnly(2026, 7, 27);
        fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 2m, "LOT-EXPIRED", today.AddDays(-10), null, batchNo: "LOT-EXPIRED", expiryDate: today.AddDays(-1));
        fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 3m, "LOT-SOON", today, null, batchNo: "LOT-SOON", expiryDate: today.AddDays(7));
        fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 4m, "LOT-LATER", today, null, batchNo: "LOT-LATER", expiryDate: today.AddDays(31));

        var alerts = fixture.Service.ExpiryAlerts(today, 30);

        Assert.Collection(alerts,
            x => { Assert.Equal("LOT-EXPIRED", x.BatchNo); Assert.True(x.IsExpired); Assert.Equal(2m, x.Quantity); },
            x => { Assert.Equal("LOT-SOON", x.BatchNo); Assert.False(x.IsExpired); Assert.Equal(3m, x.Quantity); });
        Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Service.ExpiryAlerts(today, -1));
    }

    [Fact]
    public void StagnantBatchAlerts_ReturnOnlyPositiveBatchesWithoutRecentMovement()
    {
        var fixture = new InventoryFixture(1m);
        var today = new DateOnly(2026, 7, 27);
        fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 3m, "LOT-STALE", today.AddDays(-200), null, batchNo: "LOT-STALE", expiryDate: today.AddYears(1));
        fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 4m, "LOT-RECENT", today.AddDays(-10), null, batchNo: "LOT-RECENT", expiryDate: today.AddYears(1));
        fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 2m, "LOT-ZERO", today.AddDays(-200), null, batchNo: "LOT-ZERO", expiryDate: today.AddYears(1));
        fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Outbound, 2m, "LOT-ZERO-OUT", today.AddDays(-190), null, batchNo: "LOT-ZERO", expiryDate: today.AddYears(1));

        var alerts = fixture.Service.StagnantBatchAlerts(today, 180);

        var alert = Assert.Single(alerts);
        Assert.Equal("LOT-STALE", alert.BatchNo);
        Assert.Equal(3m, alert.Quantity);
        Assert.Equal(today.AddDays(-200), alert.LastOccurredOn);
        Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Service.StagnantBatchAlerts(today, 0));
    }

    [Fact]
    public void List_BatchNoFilterMatchesOnlyTheRequestedBatchIgnoringCase()
    {
        var fixture = new InventoryFixture(1m);
        var date = new DateOnly(2026, 7, 27);
        fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 2m, "LOT-A-IN", date, null, batchNo: "LOT-A");
        fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 3m, "LOT-B-IN", date, null, batchNo: "LOT-B");

        var items = fixture.Service.List(batchNo: "lot-a");

        var item = Assert.Single(items);
        Assert.Equal("LOT-A-IN", item.SourceNo);
        Assert.Equal("LOT-A", item.BatchNo);
    }

    [Fact]
    public void OutboundByFifo_AllocatesEarliestExpiryFirstAndPreservesEveryBatchTrace()
    {
        var fixture = new InventoryFixture(1m);
        var date = new DateOnly(2026, 7, 27);
        fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 2m, "FIFO-LATER", date, null, batchNo: "LOT-LATER", expiryDate: date.AddDays(30));
        fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 3m, "FIFO-FIRST", date, null, batchNo: "LOT-FIRST", expiryDate: date.AddDays(10));

        var allocations = fixture.Service.OutboundByFifo(fixture.Product.Id, fixture.Warehouse.Id, null, 4m, "FIFO-OUT", date, "先进先出出库");

        Assert.Collection(allocations,
            x => { Assert.Equal("LOT-FIRST", x.BatchNo); Assert.Equal(3m, x.Quantity); Assert.Equal("FIFO-OUT-B01", x.SourceNo); },
            x => { Assert.Equal("LOT-LATER", x.BatchNo); Assert.Equal(1m, x.Quantity); Assert.Equal("FIFO-OUT-B02", x.SourceNo); });
        Assert.Contains(fixture.Service.BatchBalances(), x => x.BatchNo == "LOT-LATER" && x.Quantity == 1m);
        Assert.DoesNotContain(fixture.Service.BatchBalances(), x => x.BatchNo == "LOT-FIRST");
    }

    [Fact]
    public void OutboundByFifo_RejectsInsufficientBatchBalanceWithoutWritingAnyTransaction()
    {
        var fixture = new InventoryFixture(10m);
        var date = new DateOnly(2026, 7, 27);
        fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 2m, "FIFO-ONLY", date, null, batchNo: "LOT-ONLY", expiryDate: date.AddDays(10));

        Assert.Throws<InvalidOperationException>(() => fixture.Service.OutboundByFifo(fixture.Product.Id, fixture.Warehouse.Id, null, 3m, "FIFO-OVER", date, null));

        Assert.DoesNotContain(fixture.Transactions.Items, x => x.SourceNo.StartsWith("FIFO-OVER", StringComparison.Ordinal));
    }

    [Fact]
    public void OutboundByFifo_UsesConfiguredTransactionBoundaryForAllAllocatedBatches()
    {
        var boundary = new RecordingTransactionBoundary();
        var fixture = new InventoryFixture(1m, boundary);
        var date = new DateOnly(2026, 7, 27);
        fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 2m, "FIFO-TX-A", date, null, batchNo: "LOT-A", expiryDate: date.AddDays(10));
        fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 2m, "FIFO-TX-B", date, null, batchNo: "LOT-B", expiryDate: date.AddDays(20));

        fixture.Service.OutboundByFifo(fixture.Product.Id, fixture.Warehouse.Id, null, 3m, "FIFO-TX-OUT", date, null);

        Assert.Equal(1, boundary.ExecuteCount);
        Assert.Equal(2, fixture.Transactions.Items.Count(x => x.SourceNo.StartsWith("FIFO-TX-OUT", StringComparison.Ordinal)));
    }

    [Fact]
    public void OutboundByFifo_RejectsAllocatedSourceNumberCollisionBeforeWriting()
    {
        var fixture = new InventoryFixture(1m);
        var date = new DateOnly(2026, 7, 27);
        fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 2m, "FIFO-COLLISION-IN", date, null, batchNo: "LOT-A", expiryDate: date.AddDays(10));
        fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 2m, "FIFO-COLLISION-IN-B", date, null, batchNo: "LOT-B", expiryDate: date.AddDays(20));
        fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 1m, "FIFO-COLLISION-B02", date, null, batchNo: "LOT-EXISTING", expiryDate: date.AddDays(30));

        Assert.Throws<InvalidOperationException>(() => fixture.Service.OutboundByFifo(fixture.Product.Id, fixture.Warehouse.Id, null, 3m, "FIFO-COLLISION", date, null));

        Assert.DoesNotContain(fixture.Transactions.Items, x => x.SourceNo == "FIFO-COLLISION-B01");
        Assert.Contains(fixture.Service.BatchBalances(), x => x.BatchNo == "LOT-A" && x.Quantity == 2m);
    }

    [Fact]
    public void SerialNumber_TracksSingleUnitAcrossTransferAndStocktake()
    {
        var fixture = new InventoryFixture(1m);
        var target = new Warehouse("WH-002", "华南仓", null);
        fixture.Warehouses.Items.Add(target);
        var date = new DateOnly(2026, 7, 27);
        fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 1m, "SN-IN", date, null, serialNo: "SN-001");

        Assert.Throws<InvalidOperationException>(() => fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 1m, "SN-DUP", date, null, serialNo: "sn-001"));
        fixture.Service.Transfer(fixture.Product.Id, fixture.Warehouse.Id, null, target.Id, null, 1m, "SN-TR", date, serialNo: "SN-001");
        var adjustment = fixture.Service.Stocktake(fixture.Product.Id, target.Id, 0m, "SN-ST", date, serialNo: "SN-001");

        Assert.Equal(-1m, adjustment.SignedQuantity);
        Assert.All(fixture.Transactions.Items.Where(x => x.SourceNo.StartsWith("SN-TR", StringComparison.Ordinal)), x => Assert.Equal("SN-001", x.SerialNo));
        Assert.DoesNotContain(fixture.Service.SerialBalances(), x => x.SerialNo == "SN-001");
    }

    [Fact]
    public void SerialNumber_RejectsMultiUnitTransactionAndOutboundFromWrongWarehouse()
    {
        var fixture = new InventoryFixture(1m);
        var target = new Warehouse("WH-002", "华南仓", null);
        fixture.Warehouses.Items.Add(target);
        var date = new DateOnly(2026, 7, 27);

        Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 2m, "SN-MULTI", date, null, serialNo: "SN-002"));
        fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 1m, "SN-LOC", date, null, serialNo: "SN-003");

        Assert.Throws<InvalidOperationException>(() => fixture.Service.Create(fixture.Product.Id, target.Id, InventoryTransactionKind.Outbound, 1m, "SN-WRONG-WH", date, null, serialNo: "SN-003"));
    }

    [Fact]
    public void List_SerialNoFilterMatchesOnlyTheRequestedSerialIgnoringCase()
    {
        var fixture = new InventoryFixture(1m);
        var date = new DateOnly(2026, 7, 27);
        fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 1m, "SN-FILTER-A", date, null, serialNo: "SN-A");
        fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 1m, "SN-FILTER-B", date, null, serialNo: "SN-B");

        var items = fixture.Service.List(serialNo: "sn-a");

        var item = Assert.Single(items);
        Assert.Equal("SN-FILTER-A", item.SourceNo);
        Assert.Equal("SN-A", item.SerialNo);
    }

    [Fact]
    public void OverstockAlerts_ReturnOnlyActiveProductsAboveConfiguredMaximumInventory()
    {
        var fixture = new InventoryFixture(4m);
        fixture.Product.Edit(fixture.Product.Code, fixture.Product.Name, fixture.Product.Unit, fixture.Product.SalePrice, fixture.Product.Notes, fixture.Product.MaxPurchaseQuantity, fixture.Product.SafetyStock, fixture.Product.OtherInfo, 5m);
        var date = new DateOnly(2026, 7, 27);
        fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 2m, "OVERSTOCK-IN", date, null);

        var alert = Assert.Single(fixture.Service.OverstockAlerts());
        Assert.Equal(6m, alert.Quantity);
        Assert.Equal(5m, alert.MaxInventoryQuantity);

        fixture.Product.SetActive(false);
        Assert.Empty(fixture.Service.OverstockAlerts());
    }

    [Fact]
    public void OverstockAlerts_DoesNotAlertWhenBalanceEqualsConfiguredMaximumInventory()
    {
        var fixture = new InventoryFixture(5m);
        fixture.Product.Edit(fixture.Product.Code, fixture.Product.Name, fixture.Product.Unit, fixture.Product.SalePrice, fixture.Product.Notes, fixture.Product.MaxPurchaseQuantity, fixture.Product.SafetyStock, fixture.Product.OtherInfo, 5m);

        Assert.Empty(fixture.Service.OverstockAlerts());
    }

    [Fact]
    public void LocationProductCapacity_RejectsInboundTransferAndPositiveStocktakeAboveCapacity()
    {
        var fixture = new InventoryFixture(5m);
        var location = fixture.Warehouse.Locations.Single();
        location.SetProductCapacity(fixture.Product.Id, 3m);
        var target = new Warehouse("WH-002", "华南仓", null);
        var targetLocation = target.AddLocation("B-01", "货架一层");
        targetLocation.SetProductCapacity(fixture.Product.Id, 1m);
        fixture.Warehouses.Items.Add(target);
        var date = new DateOnly(2026, 7, 27);

        Assert.Throws<InvalidOperationException>(() => fixture.Service.Create(fixture.Product.Id, fixture.Warehouse.Id, InventoryTransactionKind.Inbound, 4m, "CAP-IN", date, null, location.Id));
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Transfer(fixture.Product.Id, fixture.Warehouse.Id, null, target.Id, targetLocation.Id, 2m, "CAP-TR", date));
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Stocktake(fixture.Product.Id, fixture.Warehouse.Id, 4m, "CAP-ST", date, location.Id));

        Assert.DoesNotContain(fixture.Transactions.Items, x => x.SourceNo.StartsWith("CAP-", StringComparison.Ordinal));
    }

    private sealed class InventoryFixture
    {
        public Product Product { get; } = new("SKU-TEST", "测试商品", "件", 10m, null);
        public Warehouse Warehouse { get; } = new("WH-001", "测试仓", null);
        public TransactionRepository Transactions { get; } = new();
        public WarehouseRepository Warehouses { get; } = new();
        public InventoryService Service { get; }

        public InventoryFixture(decimal openingQuantity, IWorkflowTransactionBoundary? transactions = null)
        {
            Product = new Product("SKU-TEST", "测试商品", "件", 10m, null);
            Warehouse = new Warehouse("WH-001", "测试仓", null);
            Warehouse.AddLocation("A-01", "货架一层");
            Warehouses.Items.Add(Warehouse);
            Transactions.Items.Add(new InventoryTransaction(Product.Id, Warehouse.Id, InventoryTransactionKind.Inbound, openingQuantity, "OPENING", DateOnly.FromDateTime(DateTime.Today), null));
            Service = new InventoryService(Transactions, new ProductRepository(Product), Warehouses, transactions);
        }
    }

    private sealed class TransactionRepository : IInventoryTransactionRepository
    {
        public List<InventoryTransaction> Items { get; } = [];
        public IReadOnlyList<InventoryTransaction> List() => Items;
        public void Add(InventoryTransaction item) => Items.Add(item);
    }

    private sealed class ProductRepository(Product item) : IProductRepository
    {
        public IReadOnlyList<Product> List() => [item];
        public void Add(Product item) { }
        public void Update(Product item) { }
        public void Remove(Guid id) { }
    }

    private sealed class WarehouseRepository : IWarehouseRepository
    {
        public List<Warehouse> Items { get; } = [];
        public IReadOnlyList<Warehouse> List() => Items;
        public void Add(Warehouse item) => Items.Add(item);
        public void Update(Warehouse item) { }
        public void Remove(Guid id) { }
        public void AddLocation(WarehouseLocation item) { }
        public void RemoveLocation(Guid id) { }
        public void UpsertLocationProductCapacity(WarehouseLocationProductCapacity item) { }
        public void RemoveLocationProductCapacity(Guid locationId, Guid productId) { }
    }

    private sealed class RecordingTransactionBoundary : IWorkflowTransactionBoundary
    {
        public int ExecuteCount { get; private set; }
        public void Execute(Action operation, Action<Exception>? afterRollback = null) { ExecuteCount++; operation(); }
    }
}
