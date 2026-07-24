using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.Warehouses;
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

    private sealed class InventoryFixture
    {
        public Product Product { get; } = new("SKU-TEST", "测试商品", "件", 10m, null);
        public Warehouse Warehouse { get; } = new("WH-001", "测试仓", null);
        public TransactionRepository Transactions { get; } = new();
        public WarehouseRepository Warehouses { get; } = new();
        public InventoryService Service { get; }

        public InventoryFixture(decimal openingQuantity)
        {
            Product = new Product("SKU-TEST", "测试商品", "件", 10m, null);
            Warehouse = new Warehouse("WH-001", "测试仓", null);
            Warehouse.AddLocation("A-01", "货架一层");
            Warehouses.Items.Add(Warehouse);
            Transactions.Items.Add(new InventoryTransaction(Product.Id, Warehouse.Id, InventoryTransactionKind.Inbound, openingQuantity, "OPENING", DateOnly.FromDateTime(DateTime.Today), null));
            Service = new InventoryService(Transactions, new ProductRepository(Product), Warehouses);
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
    }
}
