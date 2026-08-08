using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.PurchaseOrders;
using VelrixWorkHub.Application.SalesOrders;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class MomMaterialPlanningTests
{
    [Fact]
    public void SimulationExplodesSalesShortageThroughOneLevelBom()
    {
        var parent = new Product("FG-MRP-001", "MRP 成品", "套", 100, null);
        var component = new Product("RM-MRP-001", "MRP 组件", "件", 10, null);
        var version = new MomManufacturingVersion(parent.Id, "V1.0", "标准制造版本", DateOnly.FromDateTime(DateTime.Today));
        var versions = new InMemoryVersionRepository([version]);
        var components = new InMemoryComponentRepository();
        var bomComponent = new MomManufacturingComponent(version.Id, 10, component.Id, 2, 10);
        components.Add(bomComponent);
        version.Release();
        var order = new SalesOrder("SO-MRP-001", Guid.CreateVersion7(), parent.Id, DateOnly.FromDateTime(DateTime.Today), 10, 100, dueDate: DateOnly.FromDateTime(DateTime.Today.AddDays(5)));
        order.SetStatus(SalesOrderStatus.Submitted);
        var salesOrders = new InMemorySalesOrderRepository([order]);
        var service = CreateService([parent, component], versions, components, salesOrders);

        var run = service.Simulate("MRP-001", DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(30)));
        var lines = service.ListLines(run.Id);
        var parentLine = Assert.Single(lines, x => x.ProductId == parent.Id);
        var componentLine = Assert.Single(lines, x => x.ProductId == component.Id);

        Assert.Equal(10, parentLine.DemandQuantity);
        Assert.Equal(MomMaterialPlanningRecommendation.Production, parentLine.Recommendation);
        Assert.Equal(10, parentLine.RecommendationQuantity);
        Assert.Equal(22, componentLine.DemandQuantity);
        Assert.Equal(MomMaterialPlanningRecommendation.Purchase, componentLine.Recommendation);
        Assert.Contains("SO:SO-MRP-001", componentLine.SourceSummary);
    }

    [Fact]
    public void InventoryPurchaseAndOpenWorkOrderOffsetDemand()
    {
        var product = new Product("FG-MRP-002", "供给覆盖商品", "件", 100, null);
        var warehouseId = Guid.CreateVersion7();
        var inventory = new InMemoryInventoryRepository([new InventoryTransaction(product.Id, warehouseId, InventoryTransactionKind.Inbound, 6, "INV-MRP-001", DateOnly.FromDateTime(DateTime.Today), "期初")]);
        var purchase = new PurchaseOrder("PO-MRP-001", Guid.CreateVersion7(), product.Id, DateOnly.FromDateTime(DateTime.Today), 2, 10, PurchaseOrderSourceKind.Planning, "MRP-SOURCE", DateOnly.FromDateTime(DateTime.Today.AddDays(7)));
        purchase.SetStatus(PurchaseOrderStatus.Submitted);
        var workOrder = new MomWorkOrder("MO-MRP-001", product.Id, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(5)), 2);
        workOrder.SetStatus(MomWorkOrderStatus.Planned);
        var service = CreateService([product], new InMemoryVersionRepository(), new InMemoryComponentRepository(), new InMemorySalesOrderRepository(), [purchase], inventory, [workOrder]);
        var order = new SalesOrder("SO-MRP-002", Guid.CreateVersion7(), product.Id, DateOnly.FromDateTime(DateTime.Today), 10, 100, dueDate: DateOnly.FromDateTime(DateTime.Today.AddDays(5)));
        order.SetStatus(SalesOrderStatus.Submitted);
        ((InMemorySalesOrderRepository)service.SalesOrders).Add(order);

        var run = service.Simulate("MRP-002", DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(30)));
        var line = Assert.Single(service.ListLines(run.Id));

        Assert.Equal(6, line.OnHandQuantity);
        Assert.Equal(2, line.PurchaseInTransitQuantity);
        Assert.Equal(2, line.OpenWorkOrderQuantity);
        Assert.Equal(0, line.ShortageQuantity);
        Assert.Equal(MomMaterialPlanningRecommendation.None, line.Recommendation);
    }

    [Fact]
    public void ConfirmFreezesRunWithoutCreatingDownstreamDocuments()
    {
        var product = new Product("FG-MRP-003", "确认商品", "件", 100, null);
        var runs = new InMemoryRunRepository();
        var purchaseOrders = new InMemoryPurchaseOrderRepository();
        var workOrders = new InMemoryWorkOrderRepository();
        var service = CreateService([product], new InMemoryVersionRepository(), new InMemoryComponentRepository(), new InMemorySalesOrderRepository(), purchaseOrderRepository: purchaseOrders, workOrderRepository: workOrders, runs: runs);
        var run = service.Simulate("MRP-003", DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(7)));

        service.Confirm(run);

        Assert.Equal(MomMaterialPlanningRunStatus.Confirmed, run.Status);
        Assert.Empty(purchaseOrders.List());
        Assert.Empty(workOrders.List());
        Assert.Throws<InvalidOperationException>(() => service.Confirm(run));
    }

    [Fact]
    public void MultipleSalesOrdersForOneProductExpandBomOnlyOnce()
    {
        var parent = new Product("FG-MRP-004", "多订单成品", "套", 100, null);
        var component = new Product("RM-MRP-004", "多订单组件", "件", 10, null);
        var version = new MomManufacturingVersion(parent.Id, "V1.0", "标准版本", DateOnly.FromDateTime(DateTime.Today));
        var versions = new InMemoryVersionRepository([version]);
        var components = new InMemoryComponentRepository();
        components.Add(new MomManufacturingComponent(version.Id, 10, component.Id, 2));
        version.Release();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var first = new SalesOrder("SO-MRP-004-A", Guid.CreateVersion7(), parent.Id, today, 3, 100, dueDate: today.AddDays(5));
        var second = new SalesOrder("SO-MRP-004-B", Guid.CreateVersion7(), parent.Id, today, 4, 100, dueDate: today.AddDays(6));
        first.SetStatus(SalesOrderStatus.Submitted);
        second.SetStatus(SalesOrderStatus.Submitted);
        var service = CreateService([parent, component], versions, components, new InMemorySalesOrderRepository([first, second]));

        var run = service.Simulate("MRP-004", today, today.AddDays(30));
        var componentLine = Assert.Single(service.ListLines(run.Id), x => x.ProductId == component.Id);

        Assert.Equal(14, componentLine.DemandQuantity);
        Assert.Contains("SO-MRP-004-A", componentLine.SourceSummary);
        Assert.Contains("SO-MRP-004-B", componentLine.SourceSummary);
    }

    [Fact]
    public void PlanningBatchRequiresValidWindowAndUniqueNumber()
    {
        var product = new Product("FG-MRP-005", "计划边界商品", "件", 100, null);
        var service = CreateService([product], new InMemoryVersionRepository(), new InMemoryComponentRepository(), new InMemorySalesOrderRepository());
        var today = DateOnly.FromDateTime(DateTime.Today);

        Assert.Throws<ArgumentException>(() => service.Simulate("MRP-005-INVALID", today.AddDays(1), today));
        service.Simulate("MRP-005", today, today.AddDays(7));

        var error = Assert.Throws<InvalidOperationException>(() => service.Simulate("MRP-005", today, today.AddDays(7)));

        Assert.Contains("批次号已存在", error.Message);
    }

    private static TestService CreateService(
        IReadOnlyList<Product> products,
        InMemoryVersionRepository versions,
        InMemoryComponentRepository components,
        InMemorySalesOrderRepository salesOrders,
        IReadOnlyList<PurchaseOrder>? purchaseOrders = null,
        InMemoryInventoryRepository? inventory = null,
        IReadOnlyList<MomWorkOrder>? workOrders = null,
        InMemoryPurchaseOrderRepository? purchaseOrderRepository = null,
        InMemoryWorkOrderRepository? workOrderRepository = null,
        InMemoryRunRepository? runs = null)
    {
        purchaseOrderRepository ??= new InMemoryPurchaseOrderRepository(purchaseOrders?.ToList() ?? []);
        workOrderRepository ??= new InMemoryWorkOrderRepository(workOrders?.ToList() ?? []);
        var service = new MomMaterialPlanningService(runs ?? new InMemoryRunRepository(), new InMemoryLineRepository(), salesOrders, purchaseOrderRepository, inventory ?? new InMemoryInventoryRepository(), workOrderRepository, new InMemoryProductRepository(products.ToList()), versions, components);
        return new TestService(service, salesOrders, purchaseOrderRepository, workOrderRepository);
    }

    private sealed record TestService(MomMaterialPlanningService Inner, InMemorySalesOrderRepository SalesOrders, InMemoryPurchaseOrderRepository PurchaseOrders, InMemoryWorkOrderRepository WorkOrders)
    {
        public MomMaterialPlanningRun Simulate(string planNo, DateOnly referenceDate, DateOnly horizonDate) => Inner.Simulate(planNo, referenceDate, horizonDate);
        public IReadOnlyList<MomMaterialPlanningLine> ListLines(Guid runId) => Inner.ListLines(runId);
        public void Confirm(MomMaterialPlanningRun run) => Inner.Confirm(run);
    }

    private sealed class InMemoryRunRepository(IReadOnlyList<MomMaterialPlanningRun>? seed = null) : IMomMaterialPlanningRunRepository
    {
        private readonly List<MomMaterialPlanningRun> items = seed?.ToList() ?? [];
        public IReadOnlyList<MomMaterialPlanningRun> List() => items;
        public void Add(MomMaterialPlanningRun item) => items.Add(item);
        public void Update(MomMaterialPlanningRun item) { }
    }

    private sealed class InMemoryLineRepository : IMomMaterialPlanningLineRepository
    {
        private readonly List<MomMaterialPlanningLine> items = [];
        public IReadOnlyList<MomMaterialPlanningLine> List() => items;
        public void Add(MomMaterialPlanningLine item) => items.Add(item);
    }

    private sealed class InMemoryVersionRepository(IReadOnlyList<MomManufacturingVersion>? seed = null) : IMomManufacturingVersionRepository
    {
        private readonly List<MomManufacturingVersion> items = seed?.ToList() ?? [];
        public IReadOnlyList<MomManufacturingVersion> List() => items;
        public void Add(MomManufacturingVersion item) => items.Add(item);
        public void Update(MomManufacturingVersion item) { }
    }

    private sealed class InMemoryComponentRepository : IMomManufacturingComponentRepository
    {
        private readonly List<MomManufacturingComponent> items = [];
        public IReadOnlyList<MomManufacturingComponent> List() => items;
        public void Add(MomManufacturingComponent item) => items.Add(item);
        public void Update(MomManufacturingComponent item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class InMemorySalesOrderRepository(IReadOnlyList<SalesOrder>? seed = null) : ISalesOrderRepository
    {
        private readonly List<SalesOrder> items = seed?.ToList() ?? [];
        public IReadOnlyList<SalesOrder> List() => items;
        public void Add(SalesOrder item) => items.Add(item);
        public void Update(SalesOrder item) { }
    }

    private sealed class InMemoryPurchaseOrderRepository(IReadOnlyList<PurchaseOrder>? seed = null) : IPurchaseOrderRepository
    {
        private readonly List<PurchaseOrder> items = seed?.ToList() ?? [];
        public IReadOnlyList<PurchaseOrder> List() => items;
        public void Add(PurchaseOrder item) => items.Add(item);
        public void Update(PurchaseOrder item) { }
    }

    private sealed class InMemoryWorkOrderRepository(IReadOnlyList<MomWorkOrder>? seed = null) : IMomWorkOrderRepository
    {
        private readonly List<MomWorkOrder> items = seed?.ToList() ?? [];
        public IReadOnlyList<MomWorkOrder> List() => items;
        public void Add(MomWorkOrder item) => items.Add(item);
        public void Update(MomWorkOrder item) { }
    }

    private sealed class InMemoryInventoryRepository(IReadOnlyList<InventoryTransaction>? seed = null) : IInventoryTransactionRepository
    {
        private readonly List<InventoryTransaction> items = seed?.ToList() ?? [];
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
}
