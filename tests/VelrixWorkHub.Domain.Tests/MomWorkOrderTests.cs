using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.SalesOrders;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class MomWorkOrderTests
{
    [Fact]
    public void WorkOrderRequiresFullQuantityBeforeCompletion()
    {
        var item = new MomWorkOrder("MO-001", Guid.CreateVersion7(), DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(2)), 10);
        item.SetWorkCenter(Guid.CreateVersion7());

        item.SetStatus(MomWorkOrderStatus.Planned);
        item.SetStatus(MomWorkOrderStatus.Released);
        item.SetCompletedQuantity(6);

        Assert.Equal(MomWorkOrderStatus.InProgress, item.Status);
        Assert.Throws<InvalidOperationException>(() => item.SetStatus(MomWorkOrderStatus.Completed));

        item.SetCompletedQuantity(10);
        item.SetStatus(MomWorkOrderStatus.Completed);
        Assert.Equal(0, item.RemainingQuantity);
    }

    [Fact]
    public void WorkOrderCannotBeReleasedWithoutWorkCenter()
    {
        var item = new MomWorkOrder("MO-002", Guid.CreateVersion7(), DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(1)), 1);
        item.SetStatus(MomWorkOrderStatus.Planned);

        var error = Assert.Throws<InvalidOperationException>(() => item.SetStatus(MomWorkOrderStatus.Released));

        Assert.Contains("工作中心", error.Message);
    }

    [Fact]
    public void SalesOrderSourceMustMatchProductAndCannotDuplicateActiveWorkOrder()
    {
        var product = new Product("P-001", "制造商品", "台", 100, null);
        var otherProduct = new Product("P-002", "其他商品", "台", 100, null);
        var order = new SalesOrder("SO-001", Guid.CreateVersion7(), product.Id, DateOnly.FromDateTime(DateTime.Today), 5, 100);
        var products = new List<Product> { product, otherProduct };
        var orders = new List<SalesOrder> { order };
        var workOrders = new InMemoryMomWorkOrderRepository();
        var service = new MomWorkOrderService(workOrders, new InMemoryProductRepository(products), new InMemorySalesOrderRepository(orders));

        var created = service.Create("MO-SO-001", product.Id, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(3)), 5, MomWorkOrderSourceKind.SalesOrder, order.OrderNo, order.Id);

        Assert.Equal(order.Id, created.SalesOrderId);
        Assert.Throws<InvalidOperationException>(() => service.Create("MO-SO-002", product.Id, created.PlannedStart, created.PlannedEnd, 5, MomWorkOrderSourceKind.SalesOrder, order.OrderNo, order.Id));
        Assert.Throws<InvalidOperationException>(() => service.Create("MO-SO-003", otherProduct.Id, created.PlannedStart, created.PlannedEnd, 5, MomWorkOrderSourceKind.SalesOrder, order.OrderNo, order.Id));
    }

    [Fact]
    public void WorkOrderServiceRunsOperationCompletionGateBeforeChangingStatus()
    {
        var product = new Product("P-GATE", "门禁商品", "件", 100, null);
        var repository = new InMemoryMomWorkOrderRepository();
        var gate = new InMemoryOperationCompletionGate { Reject = true };
        var service = new MomWorkOrderService(repository, new InMemoryProductRepository([product]), operationCompletionGate: gate);
        var item = service.Create("MO-GATE", product.Id, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(1)), 1);
        item.SetWorkCenter(Guid.CreateVersion7()); item.SetStatus(MomWorkOrderStatus.Planned); item.SetStatus(MomWorkOrderStatus.Released); item.SetStatus(MomWorkOrderStatus.InProgress);

        Assert.Throws<InvalidOperationException>(() => service.Complete(item, 1));
        Assert.Equal(MomWorkOrderStatus.InProgress, item.Status);
        gate.Reject = false;
        service.Complete(item, 1);

        Assert.Equal(MomWorkOrderStatus.Completed, item.Status);
    }

    private sealed class InMemoryMomWorkOrderRepository : IMomWorkOrderRepository
    {
        private readonly List<MomWorkOrder> items = [];
        public IReadOnlyList<MomWorkOrder> List() => items;
        public void Add(MomWorkOrder item) => items.Add(item);
        public void Update(MomWorkOrder item) { }
    }

    private sealed class InMemoryProductRepository(List<Product> items) : IProductRepository
    {
        public IReadOnlyList<Product> List() => items;
        public void Add(Product item) => items.Add(item);
        public void Update(Product item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class InMemorySalesOrderRepository(List<SalesOrder> items) : ISalesOrderRepository
    {
        public IReadOnlyList<SalesOrder> List() => items;
        public void Add(SalesOrder item) => items.Add(item);
        public void Update(SalesOrder item) { }
    }

    private sealed class InMemoryOperationCompletionGate : IMomOperationCompletionGate
    {
        public bool Reject { get; set; }
        public void EnsureWorkOrderCanComplete(Guid workOrderId) { if (Reject) throw new InvalidOperationException("工单存在未完工工序，不能完工。"); }
    }
}
