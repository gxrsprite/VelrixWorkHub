using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Application.SalesOrders;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class MomAcceptanceTests
{
    [Fact]
    public void FatAcceptanceRequiresCompletedChecklistBeforeSubmissionAndCanPass()
    {
        var fixture = Fixture.Create(shipped: false);
        var acceptance = fixture.Service.Create(MomAcceptanceType.Fat, fixture.Order.Id, null, null, DateOnly.FromDateTime(DateTime.Today), "quality-user", notes: "FAT 首轮");
        var item = fixture.Service.AddItem(acceptance.Id, 1, "FAT-01", "安全回路", "急停和防护门动作正常");

        Assert.Throws<InvalidOperationException>(() => fixture.Service.Submit(acceptance.Id, "quality-user"));
        fixture.Service.SetItemResult(acceptance.Id, item.Id, MomAcceptanceItemResult.Passed, "现场通过", "quality-user");
        fixture.Service.Submit(acceptance.Id, "quality-user");
        fixture.Service.Complete(acceptance.Id, MomAcceptanceStatus.Passed, "customer-user", "整机功能符合验收要求。", null);

        Assert.Equal(MomAcceptanceStatus.Passed, acceptance.Status);
        Assert.Equal("customer-user", acceptance.CompletedBy);
        Assert.Equal(MomAcceptanceItemResult.Passed, Assert.Single(fixture.Service.ListItems(acceptance.Id)).Result);
    }

    [Fact]
    public void SatAcceptanceRequiresShippedOrderAndMatchingShipment()
    {
        var unshipped = Fixture.Create(shipped: false);
        Assert.Throws<InvalidOperationException>(() => unshipped.Service.Create(MomAcceptanceType.Sat, unshipped.Order.Id, null, null, DateOnly.FromDateTime(DateTime.Today), "site-user"));

        var shipped = Fixture.Create(shipped: true);
        var acceptance = shipped.Service.Create(MomAcceptanceType.Sat, shipped.Order.Id, shipped.Shipment!.Id, null, DateOnly.FromDateTime(DateTime.Today), "site-user", serialNo: "SN-ACCEPT-001");

        Assert.Equal(shipped.Shipment.Id, acceptance.ShipmentId);
        Assert.Equal(MomAcceptanceType.Sat, acceptance.AcceptanceType);
    }

    [Fact]
    public void FailedAcceptanceRequiresFailedChecklistItemAndFailureReason()
    {
        var fixture = Fixture.Create(shipped: false);
        var acceptance = fixture.Service.Create(MomAcceptanceType.Fat, fixture.Order.Id, null, null, DateOnly.FromDateTime(DateTime.Today), "quality-user");
        var item = fixture.Service.AddItem(acceptance.Id, 1, "FAT-02", "产能测试", "连续运行达到约定产能");
        fixture.Service.SetItemResult(acceptance.Id, item.Id, MomAcceptanceItemResult.Passed, null, "quality-user");
        fixture.Service.Submit(acceptance.Id, "quality-user");

        Assert.Throws<InvalidOperationException>(() => fixture.Service.Complete(acceptance.Id, MomAcceptanceStatus.Failed, "customer-user", "未通过", "未发现失败检查项"));
        Assert.Equal(MomAcceptanceStatus.Submitted, acceptance.Status);
    }

    [Fact]
    public void CompleteTransactionFailureRestoresSubmittedAcceptance()
    {
        var fixture = Fixture.Create(shipped: false, transactions: new ThrowingTransactionBoundary(5));
        var acceptance = fixture.Service.Create(MomAcceptanceType.Fat, fixture.Order.Id, null, null, DateOnly.FromDateTime(DateTime.Today), "quality-user");
        var item = fixture.Service.AddItem(acceptance.Id, 1, "FAT-03", "文件交付", "交付资料齐全");
        fixture.Service.SetItemResult(acceptance.Id, item.Id, MomAcceptanceItemResult.Passed, null, "quality-user");
        fixture.Service.Submit(acceptance.Id, "quality-user");

        Assert.Throws<InvalidOperationException>(() => fixture.Service.Complete(acceptance.Id, MomAcceptanceStatus.Passed, "customer-user", "通过", null));
        Assert.Equal(MomAcceptanceStatus.Submitted, acceptance.Status);
        Assert.Null(acceptance.CompletedBy);
    }

    private sealed class Fixture
    {
        public SalesOrder Order { get; private init; } = null!;
        public MomFinishedGoodsShipment? Shipment { get; private init; }
        public MomAcceptanceService Service { get; private init; } = null!;

        public static Fixture Create(bool shipped, IWorkflowTransactionBoundary? transactions = null)
        {
            var order = new SalesOrder("SO-ACCEPT-001", Guid.CreateVersion7(), Guid.CreateVersion7(), DateOnly.FromDateTime(DateTime.Today), 1m, 100m);
            order.SetStatus(SalesOrderStatus.Submitted);
            MomFinishedGoodsShipment? shipment = null;
            if (shipped)
            {
                shipment = new MomFinishedGoodsShipment(order.Id, Guid.CreateVersion7(), order.ProductId, Guid.CreateVersion7(), null, 1m, "SO-ACCEPT-001-OUT", DateOnly.FromDateTime(DateTime.Today));
                order.SetStatus(SalesOrderStatus.Shipped);
            }
            var orders = new InMemorySalesOrderRepository([order]);
            var shipments = new InMemoryShipmentRepository(shipment is null ? [] : [shipment]);
            var acceptances = new InMemoryAcceptanceRepository();
            var items = new InMemoryChecklistRepository();
            var projects = new InMemoryProjectRepository();
            return new Fixture { Order = order, Shipment = shipment, Service = new MomAcceptanceService(acceptances, items, orders, shipments, projects, transactions) };
        }
    }

    private sealed class InMemorySalesOrderRepository(IReadOnlyList<SalesOrder> seed) : ISalesOrderRepository
    {
        private readonly List<SalesOrder> items = seed.ToList();
        public IReadOnlyList<SalesOrder> List() => items;
        public void Add(SalesOrder item) => items.Add(item);
        public void Update(SalesOrder item) { }
    }

    private sealed class InMemoryShipmentRepository(IReadOnlyList<MomFinishedGoodsShipment> seed) : IMomFinishedGoodsShipmentRepository
    {
        private readonly List<MomFinishedGoodsShipment> items = seed.ToList();
        public IReadOnlyList<MomFinishedGoodsShipment> List() => items;
        public void Add(MomFinishedGoodsShipment item) => items.Add(item);
    }

    private sealed class InMemoryAcceptanceRepository : IMomAcceptanceRepository
    {
        private readonly List<MomAcceptance> items = [];
        public IReadOnlyList<MomAcceptance> List() => items;
        public void Add(MomAcceptance item) => items.Add(item);
        public void Update(MomAcceptance item) { }
    }

    private sealed class InMemoryChecklistRepository : IMomAcceptanceChecklistRepository
    {
        private readonly List<MomAcceptanceChecklistItem> items = [];
        public IReadOnlyList<MomAcceptanceChecklistItem> List(Guid? acceptanceId = null) => acceptanceId is Guid id ? items.Where(x => x.AcceptanceId == id).ToArray() : items;
        public void Add(MomAcceptanceChecklistItem item) => items.Add(item);
        public void Update(MomAcceptanceChecklistItem item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class InMemoryProjectRepository : IPmsProjectRepository
    {
        public IReadOnlyList<PmsProject> List() => [];
        public void Add(PmsProject item) { }
        public void Update(PmsProject item) { }
        public void Remove(Guid id) { }
    }

    private sealed class ThrowingTransactionBoundary(int failOnCall) : IWorkflowTransactionBoundary
    {
        private int callCount;
        public void Execute(Action operation, Action<Exception>? afterRollback = null)
        {
            operation();
            if (++callCount != failOnCall) return;
            var error = new InvalidOperationException("模拟验收事务失败。");
            afterRollback?.Invoke(error);
            throw error;
        }
    }
}
