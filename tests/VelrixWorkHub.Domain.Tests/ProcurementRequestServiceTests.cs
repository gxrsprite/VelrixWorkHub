using VelrixWorkHub.Application.ProcurementRequests;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.PurchaseOrders;
using VelrixWorkHub.Application.Settlements;
using VelrixWorkHub.Application.Suppliers;

namespace VelrixWorkHub.Domain.Tests;

public sealed class ProcurementRequestServiceTests
{
    [Fact]
    public void SubmitRequiresLinesAndProductRelatedLineRequiresProduct()
    {
        var repository = new ProcurementRequestRepository();
        var lines = new ProcurementLineRepository();
        var service = new ProcurementRequestService(repository, lines);
        var user = Guid.CreateVersion7();
        var item = Create(service, user, "CG-001", OaProcurementRequestType.ProductRelated);

        Assert.Throws<InvalidOperationException>(() => service.Submit(item, user));
        Assert.Throws<InvalidOperationException>(() => service.AddLine(item, user, null, "标准服务包", "产品", "标准版", 2, "套", 100, "{}"));
        var line = service.AddLine(item, user, Guid.CreateVersion7(), "标准服务包", "产品", "标准版", 2, "套", 100, "{\"source\":\"catalog\"}");
        service.Submit(item, user);

        Assert.Equal(OaProcurementRequestStatus.Submitted, item.Status);
        Assert.Equal(200, item.EstimatedAmount);
        Assert.Equal("{\"source\":\"catalog\"}", line.OtherInfo);
    }

    [Fact]
    public void NonProductRequestRejectsProductLineAndSupportsOfficeSupplyLine()
    {
        var repository = new ProcurementRequestRepository();
        var lines = new ProcurementLineRepository();
        var service = new ProcurementRequestService(repository, lines);
        var user = Guid.CreateVersion7();
        var item = Create(service, user, "CG-002", OaProcurementRequestType.OfficeSupply);

        Assert.Throws<InvalidOperationException>(() => service.AddLine(item, user, Guid.CreateVersion7(), "显示器", "办公用品", "27 英寸", 1, "台", 1200, "{}"));
        service.AddLine(item, user, null, "显示器", "办公用品", "27 英寸", 1, "台", 1200, "{}");
        service.Submit(item, user);

        Assert.Equal(OaProcurementRequestStatus.Submitted, item.Status);
        Assert.Equal(1200, item.EstimatedAmount);
    }

    [Fact]
    public void ApplicantIsolationAndDuplicateDocumentNumberAreEnforced()
    {
        var repository = new ProcurementRequestRepository();
        var service = new ProcurementRequestService(repository, new ProcurementLineRepository());
        var user = Guid.CreateVersion7();
        var otherUser = Guid.CreateVersion7();
        var item = Create(service, user, "CG-003", OaProcurementRequestType.NonProductRelated);

        Assert.Single(service.ListMine(user));
        Assert.Empty(service.ListMine(otherUser));
        Assert.Throws<UnauthorizedAccessException>(() => service.Edit(item, otherUser, "other", "采购部", "Velrix", item.DocumentNo, item.RequestType, item.RequestDate, item.RequiredDate, null, null, item.Purpose, "{}"));
        Assert.Throws<InvalidOperationException>(() => service.Create(user, "alice", "采购部", "Velrix", "cg-003", OaProcurementRequestType.NonProductRelated, item.RequestDate, item.RequiredDate, null, null, "重复采购", "{}"));
    }

    [Fact]
    public void RejectedRequestCanBeEditedAndResubmittedButLinesRemainRequired()
    {
        var repository = new ProcurementRequestRepository();
        var lines = new ProcurementLineRepository();
        var service = new ProcurementRequestService(repository, lines);
        var user = Guid.CreateVersion7();
        var item = Create(service, user, "CG-004", OaProcurementRequestType.Sourcing);
        service.AddLine(item, user, null, "包装材料", "辅料", "按样品确认", 10, "箱", 50, "{}");
        item.Submit(DateTime.Now);
        item.Reject("请补充供应商范围");
        repository.Update(item);

        service.Edit(item, user, "alice", "采购部", "Velrix", item.DocumentNo, item.RequestType, item.RequestDate, item.RequiredDate, null, "BUDGET-04", "补充供应商范围和比价要求", "{\"round\":2}");
        service.Submit(item, user);

        Assert.Equal(OaProcurementRequestStatus.Submitted, item.Status);
        Assert.Equal("BUDGET-04", item.BudgetReference);
        Assert.Null(item.RejectionReason);
    }

    [Fact]
    public void DateJsonAndLineValuesAreValidated()
    {
        var user = Guid.CreateVersion7();
        var today = DateOnly.FromDateTime(DateTime.Today);
        Assert.Throws<ArgumentException>(() => new OaProcurementRequest(user, "alice", "采购部", "Velrix", "CG-005", OaProcurementRequestType.NonProductRelated, today, today.AddDays(-1), null, null, "采购", "{}", DateTime.Now));
        Assert.Throws<ArgumentException>(() => new OaProcurementRequest(user, "alice", "采购部", "Velrix", "CG-006", OaProcurementRequestType.NonProductRelated, today, today, null, null, "采购", "[]", DateTime.Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OaProcurementRequestLine(Guid.CreateVersion7(), null, "物料", "辅料", "规格", 0, "件", 1, "{}"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OaProcurementRequestLine(Guid.CreateVersion7(), null, "物料", "辅料", "规格", 1, "件", -1, "{}"));
    }

    [Fact]
    public void ApprovedSingleProductRequestCreatesTraceablePurchaseOrderAndPreventsDuplicateSource()
    {
        var requestRepository = new ProcurementRequestRepository();
        var lineRepository = new ProcurementLineRepository();
        var requests = new ProcurementRequestService(requestRepository, lineRepository);
        var user = Guid.CreateVersion7();
        var product = new Product("SKU-PROCUREMENT-REQUEST", "采购申请产品", "件", 10m, null);
        var supplier = new Supplier("SUP-PROCUREMENT-REQUEST", "采购申请供应商", null, null, null);
        var orders = new PurchaseOrderService(new PurchaseOrderRepository(), new SupplierRepository(supplier), new ProductRepository(product), null!, null!, new SettlementRepository());
        var service = new ProcurementRequestPurchaseOrderService(requests, orders);
        var request = Create(requests, user, "CG-PO-001", OaProcurementRequestType.ProductRelated);
        requests.AddLine(request, user, product.Id, product.Name, "生产物料", "标准规格", 3, "件", 12m, "{}");
        request.Submit(DateTime.Now);
        requests.ApplyApproval(request);

        var order = service.CreateFromApprovedRequest(request.Id, "PO-CG-001", supplier.Id, 11.5m, DateOnly.FromDateTime(DateTime.Today.AddDays(30)));

        Assert.Equal(PurchaseOrderSourceKind.Requisition, order.SourceKind);
        Assert.Equal(request.DocumentNo, order.SourceDocumentNo);
        Assert.Equal(product.Id, order.ProductId);
        Assert.Equal(3m, order.Quantity);
        Assert.Equal(11.5m, order.UnitPrice);
        Assert.Throws<InvalidOperationException>(() => service.CreateFromApprovedRequest(request.Id, "PO-CG-001-RETRY", supplier.Id, 11.5m, DateOnly.FromDateTime(DateTime.Today.AddDays(30))));
    }

    [Fact]
    public void PurchaseOrderGenerationRequiresApprovedSingleProductLine()
    {
        var requestRepository = new ProcurementRequestRepository();
        var lineRepository = new ProcurementLineRepository();
        var requests = new ProcurementRequestService(requestRepository, lineRepository);
        var user = Guid.CreateVersion7();
        var product = new Product("SKU-PROCUREMENT-REJECT", "采购申请限制产品", "件", 10m, null);
        var supplier = new Supplier("SUP-PROCUREMENT-REJECT", "采购申请限制供应商", null, null, null);
        var orders = new PurchaseOrderService(new PurchaseOrderRepository(), new SupplierRepository(supplier), new ProductRepository(product), null!, null!, new SettlementRepository());
        var service = new ProcurementRequestPurchaseOrderService(requests, orders);
        var request = Create(requests, user, "CG-PO-002", OaProcurementRequestType.ProductRelated);
        requests.AddLine(request, user, product.Id, product.Name, "生产物料", "标准规格", 1, "件", 10m, "{}");
        requests.AddLine(request, user, product.Id, product.Name, "生产物料", "备用规格", 1, "件", 10m, "{}");
        request.Submit(DateTime.Now);
        requests.ApplyApproval(request);

        var error = Assert.Throws<InvalidOperationException>(() => service.CreateFromApprovedRequest(request.Id, "PO-CG-002", supplier.Id, 10m, DateOnly.FromDateTime(DateTime.Today.AddDays(30))));

        Assert.Equal("只有包含一条产品明细的已批准产品相关采购申请可以直接生成采购订单。", error.Message);
    }

    [Fact]
    public void ApprovedMultiLineRequestCreatesTraceableSplitOrdersAndAllowsRetryOnlyAfterAllAreCancelled()
    {
        var requestRepository = new ProcurementRequestRepository();
        var lineRepository = new ProcurementLineRepository();
        var requests = new ProcurementRequestService(requestRepository, lineRepository);
        var user = Guid.CreateVersion7();
        var firstProduct = new Product("SKU-PROCUREMENT-SPLIT-01", "采购拆单产品一", "件", null, null);
        var secondProduct = new Product("SKU-PROCUREMENT-SPLIT-02", "采购拆单产品二", "套", null, null);
        var supplier = new Supplier("SUP-PROCUREMENT-SPLIT", "采购拆单供应商", null, null, null);
        var orderRepository = new PurchaseOrderRepository();
        var orders = new PurchaseOrderService(orderRepository, new SupplierRepository(supplier), new ProductRepository(firstProduct, secondProduct), null!, null!, new SettlementRepository());
        var service = new ProcurementRequestPurchaseOrderService(requests, orders);
        var request = Create(requests, user, "CG-PO-SPLIT-001", OaProcurementRequestType.ProductRelated);
        var firstLine = requests.AddLine(request, user, firstProduct.Id, firstProduct.Name, "生产物料", "规格一", 3, "件", 12m, "{}");
        var secondLine = requests.AddLine(request, user, secondProduct.Id, secondProduct.Name, "生产物料", "规格二", 2, "套", 28m, "{}");
        request.Submit(DateTime.Now);
        requests.ApplyApproval(request);

        var created = service.CreateSplitOrdersFromApprovedRequest(
            request.Id,
            "PO-CG-PO-SPLIT-001",
            supplier.Id,
            new Dictionary<Guid, decimal> { [firstLine.Id] = 11.5m, [secondLine.Id] = 27m },
            DateOnly.FromDateTime(DateTime.Today.AddDays(30)));

        Assert.Equal(2, created.Count);
        Assert.Equal(new[] { firstLine.Id, secondLine.Id }.OrderBy(x => x), created.Select(x => x.SourceLineId!.Value).OrderBy(x => x));
        Assert.Equal(firstProduct.Id, created.Single(x => x.SourceLineId == firstLine.Id).ProductId);
        Assert.Equal(secondProduct.Id, created.Single(x => x.SourceLineId == secondLine.Id).ProductId);
        Assert.Equal(["PO-CG-PO-SPLIT-001-01", "PO-CG-PO-SPLIT-001-02"], created.Select(x => x.OrderNo).OrderBy(x => x).ToArray());
        Assert.All(created, order => Assert.Equal(request.DocumentNo, order.SourceDocumentNo));

        Assert.Throws<InvalidOperationException>(() => service.CreateSplitOrdersFromApprovedRequest(
            request.Id, "PO-CG-PO-SPLIT-RETRY", supplier.Id,
            new Dictionary<Guid, decimal> { [firstLine.Id] = 11.5m, [secondLine.Id] = 27m },
            DateOnly.FromDateTime(DateTime.Today.AddDays(30))));

        orders.SetStatus(created[0], PurchaseOrderStatus.Cancelled);
        Assert.Throws<InvalidOperationException>(() => service.CreateSplitOrdersFromApprovedRequest(
            request.Id, "PO-CG-PO-SPLIT-RETRY-2", supplier.Id,
            new Dictionary<Guid, decimal> { [firstLine.Id] = 11.5m, [secondLine.Id] = 27m },
            DateOnly.FromDateTime(DateTime.Today.AddDays(30))));

        orders.SetStatus(created[1], PurchaseOrderStatus.Cancelled);
        var retried = service.CreateSplitOrdersFromApprovedRequest(
            request.Id, "PO-CG-PO-SPLIT-RETRY-3", supplier.Id,
            new Dictionary<Guid, decimal> { [firstLine.Id] = 10m, [secondLine.Id] = 25m },
            DateOnly.FromDateTime(DateTime.Today.AddDays(30)));

        Assert.Equal(2, retried.Count);
        Assert.Equal(new[] { firstLine.Id, secondLine.Id }.OrderBy(x => x), retried.Select(x => x.SourceLineId!.Value).OrderBy(x => x));
    }

    private static OaProcurementRequest Create(ProcurementRequestService service, Guid user, string documentNo, OaProcurementRequestType type)
        => service.Create(user, "alice", "采购部", "Velrix 上海有限公司", documentNo, type, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(14)), null, null, "测试采购申请", "{}");

    private sealed class ProcurementRequestRepository : IOaProcurementRequestRepository
    {
        private readonly List<OaProcurementRequest> items = [];
        public IReadOnlyList<OaProcurementRequest> List(Guid? applicantUserId = null) => items.Where(x => applicantUserId is null || x.ApplicantUserId == applicantUserId).ToArray();
        public OaProcurementRequest? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public void Add(OaProcurementRequest item) => items.Add(item);
        public void Update(OaProcurementRequest item) { if (!items.Contains(item)) throw new InvalidOperationException(); }
    }

    private sealed class ProcurementLineRepository : IOaProcurementRequestLineRepository
    {
        private readonly List<OaProcurementRequestLine> items = [];
        public IReadOnlyList<OaProcurementRequestLine> List(Guid requestId) => items.Where(x => x.RequestId == requestId).ToArray();
        public OaProcurementRequestLine? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public void Add(OaProcurementRequestLine item) => items.Add(item);
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class PurchaseOrderRepository : IPurchaseOrderRepository
    {
        private readonly List<PurchaseOrder> items = [];
        public IReadOnlyList<PurchaseOrder> List() => items;
        public void Add(PurchaseOrder item) => items.Add(item);
        public void Update(PurchaseOrder item) { }
    }

    private sealed class SupplierRepository(params Supplier[] items) : ISupplierRepository
    {
        public IReadOnlyList<Supplier> List() => items;
        public void Add(Supplier item) { }
        public void Update(Supplier item) { }
        public void Remove(Guid id) { }
    }

    private sealed class ProductRepository(params Product[] items) : IProductRepository
    {
        public IReadOnlyList<Product> List() => items;
        public void Add(Product item) { }
        public void Update(Product item) { }
        public void Remove(Guid id) { }
    }

    private sealed class SettlementRepository : ISettlementRepository
    {
        public IReadOnlyList<ErpSettlement> List() => [];
        public void Add(ErpSettlement item) { }
        public void Update(ErpSettlement item) { }
    }
}
