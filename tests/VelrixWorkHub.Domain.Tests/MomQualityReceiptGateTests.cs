using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Application.PurchaseOrders;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class MomQualityReceiptGateTests
{
    [Fact]
    public void HistoricalPurchaseOrderWithoutQualityLinkRemainsReceivable()
    {
        var fixture = Fixture.Create();

        fixture.Service.EnsureCanReceive(fixture.Order.Id, fixture.Product.Id);
    }

    [Fact]
    public void PendingOrFailedReceiptInspectionBlocksAndPassedInspectionAllowsReceiving()
    {
        var fixture = Fixture.Create();
        var pending = fixture.Inspections.Create(fixture.Order.Id, MomQualityInspectionType.Iqc, fixture.Product.Id, "IQC-001");
        fixture.Service.Link(fixture.Order.Id, pending.Id);

        Assert.Throws<InvalidOperationException>(() => fixture.Service.EnsureCanReceive(fixture.Order.Id, fixture.Product.Id));
        fixture.Inspections.Record(pending, failed: true);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.EnsureCanReceive(fixture.Order.Id, fixture.Product.Id));

        var passed = fixture.Inspections.Create(fixture.Order.Id, MomQualityInspectionType.Sqc, fixture.Product.Id, "SQC-001");
        fixture.Service.Link(fixture.Order.Id, passed.Id);
        fixture.Inspections.Record(passed, failed: false);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.EnsureCanReceive(fixture.Order.Id, fixture.Product.Id));

        var retest = fixture.Inspections.Create(fixture.Order.Id, MomQualityInspectionType.Iqc, fixture.Product.Id, "IQC-002");
        fixture.Service.Link(fixture.Order.Id, retest.Id);
        fixture.Inspections.Record(retest, failed: false);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.EnsureCanReceive(fixture.Order.Id, fixture.Product.Id));
    }

    [Fact]
    public void CancelledOnlyLinkBlocksAndInvalidLinksAreRejected()
    {
        var fixture = Fixture.Create();
        var cancelled = fixture.Inspections.Create(fixture.Order.Id, MomQualityInspectionType.Iqc, fixture.Product.Id, "IQC-CANCEL");
        fixture.Service.Link(fixture.Order.Id, cancelled.Id);
        cancelled.Cancel();
        Assert.Throws<InvalidOperationException>(() => fixture.Service.EnsureCanReceive(fixture.Order.Id, fixture.Product.Id));

        var otherProduct = new Product("RM-QA-002", "其他原料", "件", 10, null);
        var wrongProduct = fixture.Inspections.Create(fixture.Order.Id, MomQualityInspectionType.Iqc, otherProduct.Id, "IQC-WRONG");
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Link(fixture.Order.Id, wrongProduct.Id));

        var fqc = fixture.Inspections.Create(fixture.Order.Id, MomQualityInspectionType.Fqc, fixture.Product.Id, "FQC-WRONG");
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Link(fixture.Order.Id, fqc.Id));
    }

    [Fact]
    public void DuplicateInspectionLinkIsRejected()
    {
        var fixture = Fixture.Create();
        var inspection = fixture.Inspections.Create(fixture.Order.Id, MomQualityInspectionType.Iqc, fixture.Product.Id, "IQC-DUP");

        fixture.Service.Link(fixture.Order.Id, inspection.Id);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Link(fixture.Order.Id, inspection.Id));
    }

    private sealed class Fixture
    {
        public Product Product { get; private init; } = null!;
        public PurchaseOrder Order { get; private init; } = null!;
        public InMemoryInspectionRepository Inspections { get; private init; } = null!;
        public MomQualityReceiptInspectionService Service { get; private init; } = null!;

        public static Fixture Create()
        {
            var product = new Product("RM-QA-001", "质量原料", "件", 20, null);
            var order = new PurchaseOrder("PO-QA-001", Guid.CreateVersion7(), product.Id, DateOnly.FromDateTime(DateTime.Today), 10, 2);
            order.SetStatus(PurchaseOrderStatus.Submitted);
            var inspections = new InMemoryInspectionRepository();
            var links = new InMemoryLinkRepository();
            return new Fixture { Product = product, Order = order, Inspections = inspections,
                Service = new MomQualityReceiptInspectionService(links, new InMemoryPurchaseOrderRepository(order), inspections) };
        }
    }

    private sealed class InMemoryPurchaseOrderRepository(PurchaseOrder order) : IPurchaseOrderRepository
    {
        private readonly List<PurchaseOrder> items = [order];
        public IReadOnlyList<PurchaseOrder> List() => items;
        public void Add(PurchaseOrder item) => items.Add(item);
        public void Update(PurchaseOrder item) { }
    }

    private sealed class InMemoryInspectionRepository : IMomQualityInspectionRepository
    {
        private readonly List<MomQualityInspection> items = [];
        public IReadOnlyList<MomQualityInspection> List() => items;
        public void Add(MomQualityInspection item) => items.Add(item);
        public void Update(MomQualityInspection item) { }
        public MomQualityInspection Create(Guid workOrderId, MomQualityInspectionType type, Guid productId, string inspectionNo)
        {
            var inspection = new MomQualityInspection(workOrderId, type, null, productId, null, null, 1, DateTime.Now, inspectionNo: inspectionNo);
            items.Add(inspection);
            return inspection;
        }
        public void Record(MomQualityInspection inspection, bool failed) => inspection.RecordResult(failed ? 0 : 1, failed ? 1 : 0, "inspector", DateTime.Now);
    }

    private sealed class InMemoryLinkRepository : IMomQualityReceiptInspectionRepository
    {
        private readonly List<MomQualityReceiptInspection> items = [];
        public IReadOnlyList<MomQualityReceiptInspection> List() => items;
        public void Add(MomQualityReceiptInspection item) => items.Add(item);
    }
}
