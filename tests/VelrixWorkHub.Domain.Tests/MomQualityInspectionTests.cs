using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class MomQualityInspectionTests
{
    [Fact]
    public void IpqcLatestResultGatesOperationCompletionAndAllowsPassedRetest()
    {
        var fixture = Fixture.Create();
        var pending = fixture.Service.Create(fixture.WorkOrder.Id, MomQualityInspectionType.Ipqc, fixture.Operation.Id, null, "B-001", null, 10);

        Assert.Throws<InvalidOperationException>(() => fixture.Service.EnsureOperationCanComplete(fixture.Operation.Id));
        fixture.Service.RecordResult(pending.Id, 8, 2, "inspector");
        Assert.Throws<InvalidOperationException>(() => fixture.Service.EnsureOperationCanComplete(fixture.Operation.Id));

        var retest = fixture.Service.Create(fixture.WorkOrder.Id, MomQualityInspectionType.Ipqc, fixture.Operation.Id, null, "B-002", null, 10);
        fixture.Service.RecordResult(retest.Id, 10, 0, "inspector");

        fixture.Service.EnsureOperationCanComplete(fixture.Operation.Id);
        Assert.Equal(MomQualityInspectionStatus.Failed, fixture.Service.List(fixture.WorkOrder.Id).Single(x => x.Id == pending.Id).Status);
        Assert.Equal(MomQualityInspectionStatus.Passed, fixture.Service.List(fixture.WorkOrder.Id).Single(x => x.Id == retest.Id).Status);
    }

    [Fact]
    public void QualityTypesRequireCorrectBindingsAndProtectDuplicatePendingBatch()
    {
        var fixture = Fixture.Create();

        Assert.Throws<InvalidOperationException>(() => fixture.Service.Create(fixture.WorkOrder.Id, MomQualityInspectionType.Iqc, null, null, null, null, 1));
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Create(fixture.WorkOrder.Id, MomQualityInspectionType.Fqc, null, Guid.CreateVersion7(), null, null, 1));
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Create(fixture.WorkOrder.Id, MomQualityInspectionType.Ipqc, null, null, null, null, 1));

        var first = fixture.Service.Create(fixture.WorkOrder.Id, MomQualityInspectionType.Iqc, null, fixture.Product.Id, "batch-1", null, 1);
        Assert.StartsWith("MQI-", first.InspectionNo, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Create(fixture.WorkOrder.Id, MomQualityInspectionType.Iqc, null, fixture.Product.Id, "BATCH-1", null, 1));
    }

    [Fact]
    public void LatestFqcResultGatesWorkOrderCompletion()
    {
        var fixture = Fixture.Create();
        var inspection = fixture.Service.Create(fixture.WorkOrder.Id, MomQualityInspectionType.Fqc, null, fixture.Product.Id, "FQC-001", null, 10);

        Assert.Throws<InvalidOperationException>(() => fixture.Service.EnsureWorkOrderCanComplete(fixture.WorkOrder.Id));
        fixture.Service.RecordResult(inspection.Id, 9, 1, "inspector");
        Assert.Throws<InvalidOperationException>(() => fixture.Service.EnsureWorkOrderCanComplete(fixture.WorkOrder.Id));

        var retest = fixture.Service.Create(fixture.WorkOrder.Id, MomQualityInspectionType.Fqc, null, fixture.Product.Id, "FQC-002", null, 10);
        fixture.Service.RecordResult(retest.Id, 10, 0, "inspector");
        fixture.Service.EnsureWorkOrderCanComplete(fixture.WorkOrder.Id);
    }

    [Fact]
    public void ResultMustAccountForWholeSampleAndCancellationIsTerminal()
    {
        var fixture = Fixture.Create();
        var inspection = fixture.Service.Create(fixture.WorkOrder.Id, MomQualityInspectionType.Ipqc, fixture.Operation.Id, null, null, null, 5);

        Assert.Throws<InvalidOperationException>(() => fixture.Service.RecordResult(inspection.Id, 4, 0, "inspector"));
        fixture.Service.Cancel(inspection.Id);
        Assert.Equal(MomQualityInspectionStatus.Cancelled, fixture.Service.List(fixture.WorkOrder.Id).Single().Status);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.RecordResult(inspection.Id, 5, 0, "inspector"));
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Cancel(inspection.Id));
    }

    [Fact]
    public void FailedPersistenceRestoresPendingInspectionResult()
    {
        var fixture = Fixture.Create(new ThrowingTransactionBoundary());
        var inspection = fixture.Service.Create(fixture.WorkOrder.Id, MomQualityInspectionType.Ipqc, fixture.Operation.Id, null, null, null, 5);

        Assert.Throws<InvalidOperationException>(() => fixture.Service.RecordResult(inspection.Id, 5, 0, "inspector"));
        var restored = fixture.Service.List(fixture.WorkOrder.Id).Single();
        Assert.Equal(MomQualityInspectionStatus.Pending, restored.Status);
        Assert.Equal(0, restored.AcceptedQuantity);
        Assert.Null(restored.Inspector);
    }

    private sealed class Fixture
    {
        public Product Product { get; private init; } = null!;
        public MomWorkOrder WorkOrder { get; private init; } = null!;
        public MomWorkOrderOperation Operation { get; private init; } = null!;
        public MomQualityInspectionService Service { get; private init; } = null!;

        public static Fixture Create(IWorkflowTransactionBoundary? transactions = null)
        {
            var product = new Product("FG-QA-001", "质量成品", "件", 100, null);
            var factory = new MomFactory("FACT-QA-001", "质量工厂");
            var center = new MomWorkCenter(factory.Id, "WC-QA-001", "质量工作中心", MomWorkCenterType.Testing, 8);
            var workOrder = new MomWorkOrder("MO-QA-001", product.Id, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(3)), 10);
            workOrder.SetWorkCenter(center.Id);
            workOrder.SetStatus(MomWorkOrderStatus.Planned);
            workOrder.SetStatus(MomWorkOrderStatus.Released);
            workOrder.SetStatus(MomWorkOrderStatus.InProgress);
            var operation = new MomWorkOrderOperation(workOrder.Id, 10, "OP-010", "质量工序", center.Id, 10);
            operation.Accept("operator", DateTime.Now);
            operation.Start(DateTime.Now);
            var workOrders = new InMemoryWorkOrderRepository([workOrder]);
            var operations = new InMemoryOperationRepository([operation]);
            var products = new InMemoryProductRepository([product]);
            var repository = new InMemoryQualityInspectionRepository();
            return new Fixture { Product = product, WorkOrder = workOrder, Operation = operation,
                Service = new MomQualityInspectionService(repository, workOrders, operations, products, transactions) };
        }
    }

    private sealed class InMemoryQualityInspectionRepository : IMomQualityInspectionRepository
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

    private sealed class ThrowingTransactionBoundary : IWorkflowTransactionBoundary
    {
        private int calls;
        public void Execute(Action operation, Action<Exception>? afterRollback = null)
        {
            operation();
            if (++calls == 1) return;
            var exception = new InvalidOperationException("模拟质量检验持久化失败。");
            afterRollback?.Invoke(exception);
            throw exception;
        }
    }
}
