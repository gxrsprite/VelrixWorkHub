using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class MomQualityNonconformanceTests
{
    [Fact]
    public void FailedInspectionCreatesOneNonconformanceWithRejectedQuantity()
    {
        var fixture = Fixture.Create();
        var item = fixture.Service.CreateFromFailedInspection(fixture.Inspection.Id, "DIM-OUT", "Length is out of tolerance", MomQualityNonconformanceSeverity.Major);

        Assert.Equal(fixture.Inspection.Id, item.InspectionId);
        Assert.Equal(3m, item.Quantity);
        Assert.Equal(MomQualityNonconformanceStatus.Open, item.Status);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.CreateFromFailedInspection(fixture.Inspection.Id, "SECOND", "Duplicate", MomQualityNonconformanceSeverity.Minor));
    }

    [Fact]
    public void ReworkDispositionRequiresMatchingTargetAndCanCloseTheNonconformance()
    {
        var fixture = Fixture.Create();
        var item = fixture.Service.CreateFromFailedInspection(fixture.Inspection.Id, "DIM-OUT", "Length is out of tolerance", MomQualityNonconformanceSeverity.Major);

        var disposition = fixture.Service.CreateDisposition(item.Id, MomQualityDispositionAction.Rework, fixture.TargetWorkOrder.Id, fixture.TargetOperation.Id);
        Assert.Equal(MomQualityNonconformanceStatus.DispositionPlanned, item.Status);
        Assert.Equal(MomQualityDispositionStatus.Planned, disposition.Status);

        fixture.Service.CompleteDisposition(disposition.Id, "quality-manager", notes: "Rework accepted");
        Assert.Equal(MomQualityDispositionStatus.Completed, disposition.Status);
        Assert.Equal(MomQualityNonconformanceStatus.Closed, item.Status);
        Assert.Equal("quality-manager", item.ClosedBy);
    }

    [Fact]
    public void NonReworkDispositionHasNoTargetAndCancellationReopensTheRecord()
    {
        var fixture = Fixture.Create();
        var item = fixture.Service.CreateFromFailedInspection(fixture.Inspection.Id, "SURFACE", "Scratch detected", MomQualityNonconformanceSeverity.Minor);

        var disposition = fixture.Service.CreateDisposition(item.Id, MomQualityDispositionAction.Scrap);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.CreateDisposition(item.Id, MomQualityDispositionAction.UseAsIs));
        fixture.Service.CancelDisposition(disposition.Id);

        Assert.Equal(MomQualityDispositionStatus.Cancelled, disposition.Status);
        Assert.Equal(MomQualityNonconformanceStatus.Open, item.Status);
        Assert.Throws<ArgumentException>(() => fixture.Service.CreateDisposition(item.Id, MomQualityDispositionAction.Scrap, fixture.TargetWorkOrder.Id, null));
    }

    [Fact]
    public void FailedDispositionTransactionRestoresNonconformanceState()
    {
        var fixture = Fixture.Create();
        var item = fixture.Service.CreateFromFailedInspection(fixture.Inspection.Id, "DIM-OUT", "Length is out of tolerance", MomQualityNonconformanceSeverity.Major);
        var failing = fixture.WithTransaction(new ThrowingTransactionBoundary());

        Assert.Throws<InvalidOperationException>(() => failing.CreateDisposition(item.Id, MomQualityDispositionAction.Rework, fixture.TargetWorkOrder.Id, fixture.TargetOperation.Id));
        Assert.Equal(MomQualityNonconformanceStatus.Open, item.Status);
        Assert.Null(item.DispositionId);
    }

    private sealed class Fixture
    {
        public Product Product { get; private init; } = null!;
        public MomWorkOrder WorkOrder { get; private init; } = null!;
        public MomWorkOrder TargetWorkOrder { get; private init; } = null!;
        public MomWorkOrderOperation Operation { get; private init; } = null!;
        public MomWorkOrderOperation TargetOperation { get; private init; } = null!;
        public MomQualityInspection Inspection { get; private init; } = null!;
        public InMemoryNonconformanceRepository Nonconformances { get; private init; } = null!;
        public InMemoryDispositionRepository Dispositions { get; private init; } = null!;
        public MomQualityNonconformanceService Service { get; private init; } = null!;

        public static Fixture Create()
        {
            var product = new Product("FG-NCR-001", "NCR product", "pcs", 100, null);
            var factory = new MomFactory("FACT-NCR-001", "NCR factory");
            var center = new MomWorkCenter(factory.Id, "WC-NCR-001", "NCR center", MomWorkCenterType.Testing, 8);
            var workOrder = BuildWorkOrder("MO-NCR-001", product.Id, center.Id);
            var targetWorkOrder = BuildWorkOrder("MO-NCR-REWORK-001", product.Id, center.Id);
            var operation = BuildOperation(workOrder.Id, center.Id, "OP-NCR-010");
            var targetOperation = BuildOperation(targetWorkOrder.Id, center.Id, "OP-REWORK-010");
            var inspection = new MomQualityInspection(workOrder.Id, MomQualityInspectionType.Ipqc, operation.Id, null, "B-NCR-001", null, 10, DateTime.Now);
            inspection.RecordResult(7, 3, "inspector", DateTime.Now);
            var inspections = new InMemoryInspectionRepository([inspection]);
            var workOrders = new InMemoryWorkOrderRepository([workOrder, targetWorkOrder]);
            var operations = new InMemoryOperationRepository([operation, targetOperation]);
            var nonconformances = new InMemoryNonconformanceRepository();
            var dispositions = new InMemoryDispositionRepository();
            var service = new MomQualityNonconformanceService(nonconformances, dispositions, inspections, workOrders, operations);
            return new Fixture { Product = product, WorkOrder = workOrder, TargetWorkOrder = targetWorkOrder, Operation = operation, TargetOperation = targetOperation,
                Inspection = inspection, Nonconformances = nonconformances, Dispositions = dispositions, Service = service };
        }

        public MomQualityNonconformanceService WithTransaction(IWorkflowTransactionBoundary transaction)
            => new(Nonconformances, Dispositions, new InMemoryInspectionRepository([Inspection]), new InMemoryWorkOrderRepository([WorkOrder, TargetWorkOrder]), new InMemoryOperationRepository([Operation, TargetOperation]), transaction);

        private static MomWorkOrder BuildWorkOrder(string number, Guid productId, Guid centerId)
        {
            var item = new MomWorkOrder(number, productId, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(3)), 10);
            item.SetWorkCenter(centerId); item.SetStatus(MomWorkOrderStatus.Planned); item.SetStatus(MomWorkOrderStatus.Released); item.SetStatus(MomWorkOrderStatus.InProgress); return item;
        }

        private static MomWorkOrderOperation BuildOperation(Guid workOrderId, Guid centerId, string code)
        {
            var item = new MomWorkOrderOperation(workOrderId, 10, code, code, centerId, 10); item.Accept("operator", DateTime.Now); item.Start(DateTime.Now); return item;
        }
    }

    private sealed class InMemoryInspectionRepository(IReadOnlyList<MomQualityInspection> seed) : IMomQualityInspectionRepository
    {
        private readonly List<MomQualityInspection> items = seed.ToList();
        public IReadOnlyList<MomQualityInspection> List() => items;
        public void Add(MomQualityInspection item) => items.Add(item);
        public void Update(MomQualityInspection item) { }
    }

    private sealed class InMemoryNonconformanceRepository : IMomQualityNonconformanceRepository
    {
        private readonly List<MomQualityNonconformance> items = [];
        public IReadOnlyList<MomQualityNonconformance> List() => items;
        public void Add(MomQualityNonconformance item) => items.Add(item);
        public void Update(MomQualityNonconformance item) { }
    }

    private sealed class InMemoryDispositionRepository : IMomQualityDispositionRepository
    {
        private readonly List<MomQualityDisposition> items = [];
        public IReadOnlyList<MomQualityDisposition> List() => items;
        public void Add(MomQualityDisposition item) => items.Add(item);
        public void Update(MomQualityDisposition item) { }
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

    private sealed class ThrowingTransactionBoundary : IWorkflowTransactionBoundary
    {
        public void Execute(Action operation, Action<Exception>? afterRollback = null)
        {
            operation(); var exception = new InvalidOperationException("模拟不合格处置事务失败。"); afterRollback?.Invoke(exception); throw exception;
        }
    }
}
