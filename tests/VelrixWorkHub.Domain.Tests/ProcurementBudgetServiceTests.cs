using VelrixWorkHub.Application.ProcurementRequests;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.PurchaseOrders;
using VelrixWorkHub.Application.Settlements;
using VelrixWorkHub.Application.Suppliers;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class ProcurementBudgetServiceTests
{
    [Fact]
    public void SubmitReserves_RejectionReleases_AndOrderExecutionConsumesBudget()
    {
        var budgetRepository = new BudgetRepository();
        var reservationRepository = new ReservationRepository();
        var budgets = new ProcurementBudgetService(budgetRepository, reservationRepository);
        var budget = budgets.Create("PURCHASE-BUDGET-001", "Velrix", "采购部", 300, "{}", canManage: true);
        var requests = new RequestRepository();
        var lines = new LineRepository();
        var procurementRequests = new ProcurementRequestService(requests, lines, budgets: budgets);
        var userId = Guid.CreateVersion7();
        var request = procurementRequests.Create(userId, "申请人", "采购部", "Velrix", "CG-BUDGET-001", OaProcurementRequestType.NonProductRelated,
            Today, Today, null, budget.BudgetNo, "办公用品采购", "{}");
        procurementRequests.AddLine(request, userId, null, "办公用品", "办公", "标准规格", 2, "件", 100, "{}");

        procurementRequests.Submit(request, userId);
        Assert.Equal(200m, budget.ReservedAmount);
        Assert.Equal(OaProcurementBudgetReservationStatus.Reserved, reservationRepository.GetByProcurementRequest(request.Id)!.Status);

        procurementRequests.ApplyRejection(request, "预算说明补充");
        Assert.Equal(0m, budget.ReservedAmount);
        Assert.Equal(OaProcurementBudgetReservationStatus.Released, reservationRepository.GetByProcurementRequest(request.Id)!.Status);

        procurementRequests.Submit(request, userId);
        procurementRequests.ApplyApproval(request);
        budgets.ConsumeForOrder(request);

        Assert.Equal(0m, budget.ReservedAmount);
        Assert.Equal(200m, budget.ConsumedAmount);
        Assert.Equal(OaProcurementBudgetReservationStatus.Consumed, reservationRepository.GetByProcurementRequest(request.Id)!.Status);
        budgets.ReleaseForCancelledOrder(request);
        Assert.Equal(0m, budget.ConsumedAmount);
        Assert.Equal(OaProcurementBudgetReservationStatus.Released, reservationRepository.GetByProcurementRequest(request.Id)!.Status);
        budgets.PrepareForOrder(request);
        budgets.ConsumeForOrder(request);
        Assert.Equal(200m, budget.ConsumedAmount);
        Assert.Equal(100m, budget.AvailableAmount);
    }

    [Fact]
    public void SubmitRejectsInsufficientOrMismatchedBudgetAndCancelReleasesReservation()
    {
        var budgetRepository = new BudgetRepository();
        var reservationRepository = new ReservationRepository();
        var budgets = new ProcurementBudgetService(budgetRepository, reservationRepository);
        budgets.Create("PURCHASE-BUDGET-002", "Velrix", "采购部", 50, "{}", canManage: true);
        var requests = new RequestRepository();
        var lines = new LineRepository();
        var procurementRequests = new ProcurementRequestService(requests, lines, budgets: budgets);
        var userId = Guid.CreateVersion7();

        var tooLarge = Create(procurementRequests, lines, userId, "CG-BUDGET-002", "采购部", 60, "PURCHASE-BUDGET-002");
        Assert.Throws<InvalidOperationException>(() => procurementRequests.Submit(tooLarge, userId));
        Assert.Equal(OaProcurementRequestStatus.Draft, tooLarge.Status);

        var mismatched = Create(procurementRequests, lines, userId, "CG-BUDGET-003", "行政部", 10, "PURCHASE-BUDGET-002");
        Assert.Throws<InvalidOperationException>(() => procurementRequests.Submit(mismatched, userId));
        Assert.Equal(0m, budgetRepository.List().Single().ReservedAmount);

        var valid = Create(procurementRequests, lines, userId, "CG-BUDGET-004", "采购部", 10, "PURCHASE-BUDGET-002");
        procurementRequests.Submit(valid, userId);
        procurementRequests.Cancel(valid, userId, "申请人");
        Assert.Equal(0m, budgetRepository.List().Single().ReservedAmount);
        Assert.Equal(OaProcurementBudgetReservationStatus.Released, reservationRepository.GetByProcurementRequest(valid.Id)!.Status);
    }

    [Fact]
    public void OrderExecutionRequiresApprovedRequestAndActiveReservation()
    {
        var budgets = new ProcurementBudgetService(new BudgetRepository(), new ReservationRepository());
        var budget = budgets.Create("PURCHASE-BUDGET-003", "Velrix", "采购部", 100, "{}", canManage: true);
        var request = new OaProcurementRequest(Guid.CreateVersion7(), "申请人", "采购部", "Velrix", "CG-BUDGET-005",
            OaProcurementRequestType.NonProductRelated, Today, Today, null, budget.BudgetNo, "采购", "{}", DateTime.Now);
        request.SetEstimatedAmount(20);
        Assert.Throws<InvalidOperationException>(() => budgets.ConsumeForOrder(request));

        request.Submit(DateTime.Now);
        request.Approve();
        budgets.ReserveForSubmission(request);
        budgets.ReleaseForRequest(request);
        Assert.Throws<InvalidOperationException>(() => budgets.ConsumeForOrder(request));
    }

    [Fact]
    public void GeneratingRequisitionConsumesBudgetAndCancellingOrderRestoresItForRetry()
    {
        var budgetRepository = new BudgetRepository();
        var reservationRepository = new ReservationRepository();
        var budgets = new ProcurementBudgetService(budgetRepository, reservationRepository);
        var budget = budgets.Create("PURCHASE-BUDGET-005", "Velrix", "采购部", 200, "{}", canManage: true);
        var requestRepository = new RequestRepository();
        var lineRepository = new LineRepository();
        var requests = new ProcurementRequestService(requestRepository, lineRepository, budgets: budgets);
        var userId = Guid.CreateVersion7();
        var product = new Product("SKU-PURCHASE-BUDGET", "预算商品", "件", 10, null);
        var supplier = new Supplier("SUP-PURCHASE-BUDGET", "预算供应商", null, null, null);
        var orderRepository = new PurchaseOrderRepository();
        var orders = new PurchaseOrderService(orderRepository, new SupplierRepository(supplier), new ProductRepository(product), null!, null!, new SettlementRepository(),
            procurementRequests: requestRepository, procurementBudgets: budgets);
        var purchaseOrderService = new ProcurementRequestPurchaseOrderService(requests, orders, budgets);
        var request = requests.Create(userId, "申请人", "采购部", "Velrix", "CG-BUDGET-005", OaProcurementRequestType.ProductRelated,
            Today, Today, null, budget.BudgetNo, "采购商品", "{}");
        requests.AddLine(request, userId, product.Id, product.Name, "物料", "标准", 2, "件", 50, "{}");
        requests.Submit(request, userId);
        requests.ApplyApproval(request);

        var first = purchaseOrderService.CreateFromApprovedRequest(request.Id, "PO-BUDGET-005", supplier.Id, 60, Today.AddDays(30));
        Assert.Equal(100m, budget.ConsumedAmount);
        orders.SetStatus(first, PurchaseOrderStatus.Cancelled);
        Assert.Equal(0m, budget.ConsumedAmount);
        Assert.Equal(200m, budget.AvailableAmount);

        purchaseOrderService.CreateFromApprovedRequest(request.Id, "PO-BUDGET-005-RETRY", supplier.Id, 60, Today.AddDays(30));
        Assert.Equal(100m, budget.ConsumedAmount);
    }

    [Fact]
    public void ClosingRequiresNoOutstandingReservationAndPermission()
    {
        var budgetRepository = new BudgetRepository();
        var reservationRepository = new ReservationRepository();
        var budgets = new ProcurementBudgetService(budgetRepository, reservationRepository);
        var budget = budgets.Create("PURCHASE-BUDGET-004", "Velrix", "采购部", 100, "{}", canManage: true);
        var requestId = Guid.CreateVersion7();
        var reservation = new OaProcurementBudgetReservation(budget.Id, requestId, 20, DateTime.Now);
        budget.Reserve(20);
        budgetRepository.Update(budget);
        reservationRepository.Add(reservation);

        Assert.Throws<UnauthorizedAccessException>(() => budgets.Close(budget, canManage: false));
        Assert.Throws<InvalidOperationException>(() => budgets.Close(budget, canManage: true));
        reservation.Release();
        budget.Release(20);
        reservationRepository.Update(reservation);
        budgetRepository.Update(budget);
        budgets.Close(budget, canManage: true);
        Assert.Equal(OaProcurementBudgetStatus.Closed, budget.Status);
    }

    private static OaProcurementRequest Create(ProcurementRequestService service, LineRepository lines, Guid userId,
        string documentNo, string department, decimal amount, string budgetReference)
    {
        var request = service.Create(userId, "申请人", department, "Velrix", documentNo, OaProcurementRequestType.NonProductRelated,
            Today, Today, null, budgetReference, "采购", "{}");
        service.AddLine(request, userId, null, "物品", "办公", "规格", 1, "件", amount, "{}");
        return request;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    private sealed class RequestRepository : IOaProcurementRequestRepository
    {
        private readonly List<OaProcurementRequest> items = [];
        public IReadOnlyList<OaProcurementRequest> List(Guid? applicantUserId = null) => items.Where(x => applicantUserId is null || x.ApplicantUserId == applicantUserId.Value).ToArray();
        public OaProcurementRequest? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public void Add(OaProcurementRequest item) => items.Add(item);
        public void Update(OaProcurementRequest item) { }
    }

    private sealed class LineRepository : IOaProcurementRequestLineRepository
    {
        private readonly List<OaProcurementRequestLine> items = [];
        public IReadOnlyList<OaProcurementRequestLine> List(Guid requestId) => items.Where(x => x.RequestId == requestId).ToArray();
        public OaProcurementRequestLine? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public void Add(OaProcurementRequestLine item) => items.Add(item);
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class BudgetRepository : IOaProcurementBudgetRepository
    {
        private readonly List<OaProcurementBudget> items = [];
        public IReadOnlyList<OaProcurementBudget> List() => items;
        public OaProcurementBudget? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public void Add(OaProcurementBudget item) => items.Add(item);
        public void Update(OaProcurementBudget item) { }
    }

    private sealed class ReservationRepository : IOaProcurementBudgetReservationRepository
    {
        private readonly List<OaProcurementBudgetReservation> items = [];
        public IReadOnlyList<OaProcurementBudgetReservation> List(Guid? budgetId = null) => items.Where(x => budgetId is null || x.BudgetId == budgetId.Value).ToArray();
        public OaProcurementBudgetReservation? GetByProcurementRequest(Guid procurementRequestId) => items.FirstOrDefault(x => x.ProcurementRequestId == procurementRequestId);
        public void Add(OaProcurementBudgetReservation item) => items.Add(item);
        public void Update(OaProcurementBudgetReservation item) { }
    }

    private sealed class PurchaseOrderRepository : IPurchaseOrderRepository
    {
        private readonly List<PurchaseOrder> items = [];
        public IReadOnlyList<PurchaseOrder> List() => items;
        public void Add(PurchaseOrder item) => items.Add(item);
        public void Update(PurchaseOrder item) { }
    }

    private sealed class SupplierRepository(params Supplier[] initial) : ISupplierRepository
    {
        private readonly List<Supplier> items = [.. initial];
        public IReadOnlyList<Supplier> List() => items;
        public void Add(Supplier item) => items.Add(item);
        public void Update(Supplier item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class ProductRepository(params Product[] initial) : IProductRepository
    {
        private readonly List<Product> items = [.. initial];
        public IReadOnlyList<Product> List() => items;
        public void Add(Product item) => items.Add(item);
        public void Update(Product item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class SettlementRepository : ISettlementRepository
    {
        public IReadOnlyList<ErpSettlement> List() => [];
        public void Add(ErpSettlement item) { }
        public void Update(ErpSettlement item) { }
    }
}
