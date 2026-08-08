using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class MomQualityInspectionStandardTests
{
    [Fact]
    public void PublishedStandardRequiresItemsAndCannotBeEdited()
    {
        var fixture = Fixture.Create();
        var standard = fixture.Service.Create(fixture.Product.Id, MomQualityInspectionType.Iqc, "IQC-RAW", "Incoming material", "1.0");

        Assert.Throws<InvalidOperationException>(() => fixture.Service.Publish(standard));
        fixture.Service.AddItem(standard.Id, 10, "DIM-001", "Length", "10 ± 0.2", "mm", 9.8m, 10.2m);
        fixture.Service.Publish(standard);

        Assert.Equal(MomQualityInspectionStandardStatus.Active, standard.Status);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Edit(standard, fixture.Product.Id, MomQualityInspectionType.Iqc, "IQC-RAW", "Changed", "2.0"));
        Assert.Throws<InvalidOperationException>(() => fixture.Service.AddItem(standard.Id, 20, "SUR-001", "Surface", "No scratches", null, null, null));
    }

    [Fact]
    public void QualityInspectionFreezesPublishedStandardSnapshot()
    {
        var fixture = Fixture.Create();
        var standard = fixture.Service.Create(null, MomQualityInspectionType.Ipqc, "IPQC-FG", "Process check", "2026.08");
        fixture.Service.AddItem(standard.Id, 10, "TORQUE", "Torque", "8 to 12", "N·m", 8m, 12m);
        fixture.Service.Publish(standard);

        var inspection = fixture.Quality.Create(fixture.WorkOrder.Id, MomQualityInspectionType.Ipqc, fixture.Operation.Id, null,
            "BATCH-001", null, 1, standardId: standard.Id);

        Assert.Equal(standard.Id, inspection.StandardId);
        Assert.Equal("IPQC-FG", inspection.StandardCode);
        Assert.Equal("2026.08", inspection.StandardVersion);
        Assert.Contains("TORQUE", inspection.StandardSnapshotJson, StringComparison.Ordinal);
        Assert.Contains("8", inspection.StandardSnapshotJson, StringComparison.Ordinal);
    }

    [Fact]
    public void StandardSelectionRejectsWrongTypeOrProduct()
    {
        var fixture = Fixture.Create();
        var standard = fixture.Service.Create(fixture.Product.Id, MomQualityInspectionType.Iqc, "IQC-RAW", "Incoming material", "1.0");
        fixture.Service.AddItem(standard.Id, 10, "COLOR", "Color", "Pass", null, null, null);
        fixture.Service.Publish(standard);

        Assert.Null(fixture.Service.GetActiveSnapshot(standard.Id, MomQualityInspectionType.Fqc, fixture.Product.Id));
        Assert.Null(fixture.Service.GetActiveSnapshot(standard.Id, MomQualityInspectionType.Iqc, Guid.CreateVersion7()));
    }

    [Fact]
    public void DuplicateStandardAndItemKeysAreRejected()
    {
        var fixture = Fixture.Create();
        var first = fixture.Service.Create(fixture.Product.Id, MomQualityInspectionType.Iqc, "IQC-RAW", "Incoming material", "1.0");
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Create(fixture.Product.Id, MomQualityInspectionType.Iqc, "iqc-raw", "Duplicate", "1.0"));

        fixture.Service.AddItem(first.Id, 10, "COLOR", "Color", "Pass", null, null, null);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.AddItem(first.Id, 10, "SIZE", "Size", "Pass", null, null, null));
        Assert.Throws<InvalidOperationException>(() => fixture.Service.AddItem(first.Id, 20, "color", "Color 2", "Pass", null, null, null));
    }

    [Fact]
    public void StandardStateRestoresWhenTransactionFails()
    {
        var fixture = Fixture.Create();
        var standard = fixture.Service.Create(fixture.Product.Id, MomQualityInspectionType.Iqc, "IQC-TX", "Incoming material", "1.0");
        fixture.Service.AddItem(standard.Id, 10, "COLOR", "Color", "Pass", null, null, null);
        var failingService = new MomQualityInspectionStandardService(fixture.Standards, fixture.Items, fixture.Products, new ThrowingTransactionBoundary());

        Assert.Throws<InvalidOperationException>(() => failingService.Publish(standard));
        Assert.Equal(MomQualityInspectionStandardStatus.Draft, standard.Status);
        Assert.Throws<InvalidOperationException>(() => failingService.Edit(standard, fixture.Product.Id, MomQualityInspectionType.Iqc, "IQC-TX-NEW", "Changed", "2.0"));
        Assert.Equal("IQC-TX", standard.Code);

        fixture.Service.Publish(standard);
        Assert.Throws<InvalidOperationException>(() => failingService.Retire(standard));
        Assert.Equal(MomQualityInspectionStandardStatus.Active, standard.Status);
    }

    private sealed class Fixture
    {
        public Product Product { get; private init; } = null!;
        public MomWorkOrder WorkOrder { get; private init; } = null!;
        public MomWorkOrderOperation Operation { get; private init; } = null!;
        public MomQualityInspectionStandardService Service { get; private init; } = null!;
        public MomQualityInspectionService Quality { get; private init; } = null!;
        public InMemoryStandardRepository Standards { get; private init; } = null!;
        public InMemoryItemRepository Items { get; private init; } = null!;
        public InMemoryProductRepository Products { get; private init; } = null!;

        public static Fixture Create()
        {
            var product = new Product("FG-QA-STD", "Quality product", "pcs", 100, null);
            var factory = new MomFactory("FACT-QA-STD", "Quality factory");
            var center = new MomWorkCenter(factory.Id, "WC-QA-STD", "Quality center", MomWorkCenterType.Testing, 8);
            var workOrder = new MomWorkOrder("MO-QA-STD", product.Id, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(3)), 10);
            workOrder.SetWorkCenter(center.Id); workOrder.SetStatus(MomWorkOrderStatus.Planned); workOrder.SetStatus(MomWorkOrderStatus.Released); workOrder.SetStatus(MomWorkOrderStatus.InProgress);
            var operation = new MomWorkOrderOperation(workOrder.Id, 10, "OP-STD", "Quality operation", center.Id, 10);
            operation.Accept("operator", DateTime.Now); operation.Start(DateTime.Now);
            var products = new InMemoryProductRepository([product]);
            var standards = new InMemoryStandardRepository();
            var items = new InMemoryItemRepository();
            var service = new MomQualityInspectionStandardService(standards, items, products);
            var quality = new MomQualityInspectionService(new InMemoryInspectionRepository(), new InMemoryWorkOrderRepository([workOrder]), new InMemoryOperationRepository([operation]), products, standardService: service);
            return new Fixture { Product = product, WorkOrder = workOrder, Operation = operation, Service = service, Quality = quality, Standards = standards, Items = items, Products = products };
        }
    }

    private sealed class InMemoryStandardRepository : IMomQualityInspectionStandardRepository
    {
        private readonly List<MomQualityInspectionStandard> items = [];
        public IReadOnlyList<MomQualityInspectionStandard> List() => items;
        public void Add(MomQualityInspectionStandard item) => items.Add(item);
        public void Update(MomQualityInspectionStandard item) { }
    }

    private sealed class InMemoryItemRepository : IMomQualityInspectionStandardItemRepository
    {
        private readonly List<MomQualityInspectionStandardItem> items = [];
        public IReadOnlyList<MomQualityInspectionStandardItem> List() => items;
        public void Add(MomQualityInspectionStandardItem item) => items.Add(item);
        public void Update(MomQualityInspectionStandardItem item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class InMemoryInspectionRepository : IMomQualityInspectionRepository
    {
        private readonly List<MomQualityInspection> items = [];
        public IReadOnlyList<MomQualityInspection> List() => items;
        public void Add(MomQualityInspection item) => items.Add(item);
        public void Update(MomQualityInspection item) { }
    }

    private sealed class InMemoryWorkOrderRepository(IReadOnlyList<MomWorkOrder> seed) : IMomWorkOrderRepository
    {
        private readonly List<MomWorkOrder> items = seed.ToList();
        public IReadOnlyList<MomWorkOrder> List() => items;
        public void Add(MomWorkOrder item) => items.Add(item);
        public void Update(MomWorkOrder item) { }
    }

    private sealed class InMemoryOperationRepository(IReadOnlyList<MomWorkOrderOperation> seed) : IMomWorkOrderOperationRepository
    {
        private readonly List<MomWorkOrderOperation> items = seed.ToList();
        public IReadOnlyList<MomWorkOrderOperation> List() => items;
        public void Add(MomWorkOrderOperation item) => items.Add(item);
        public void Update(MomWorkOrderOperation item) { }
    }

    private sealed class InMemoryProductRepository(IReadOnlyList<Product> seed) : IProductRepository
    {
        private readonly List<Product> items = seed.ToList();
        public IReadOnlyList<Product> List() => items;
        public void Add(Product item) => items.Add(item);
        public void Update(Product item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class ThrowingTransactionBoundary : VelrixWorkHub.Application.Workflow.IWorkflowTransactionBoundary
    {
        public void Execute(Action operation, Action<Exception>? afterRollback = null)
        {
            operation();
            var exception = new InvalidOperationException("模拟质量标准事务失败。");
            afterRollback?.Invoke(exception);
            throw exception;
        }
    }
}
