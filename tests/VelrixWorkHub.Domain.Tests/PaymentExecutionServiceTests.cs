using VelrixWorkHub.Application.PaymentRequests;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.PurchaseOrders;
using VelrixWorkHub.Application.SalesOrders;
using VelrixWorkHub.Application.Settlements;
using VelrixWorkHub.Application.Suppliers;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PaymentExecutionServiceTests
{
    [Fact]
    public void SupplierPayment_RegistersExecutionAndErpPayableSettlementIdempotently()
    {
        var userId = Guid.CreateVersion7();
        var supplier = new Supplier("SUP-001", "测试供应商", null, null, null);
        var order = new PurchaseOrder("PO-PAY-001", supplier.Id, Guid.CreateVersion7(), DateOnly.FromDateTime(DateTime.Today), 2, 50, PurchaseOrderSourceKind.Manual);
        order.SetStatus(PurchaseOrderStatus.Submitted);
        var purchaseOrders = new PurchaseOrderRepository(order);
        var settlements = new SettlementRepository();
        var requestRepository = new PaymentRequestRepository();
        var paymentRequests = new PaymentRequestService(requestRepository);
        var request = paymentRequests.Create(userId, "申请人", "财务部", "Velrix", "FK-PAY-001", supplier.Name, "末四位 1234", "工商银行", "CNY", 100,
            OaPaymentRequestType.SupplierPayment, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today), order.OrderNo, null, "采购付款", "{}");
        paymentRequests.Submit(request, userId);
        paymentRequests.ApplyApproval(request);
        paymentRequests.ReviewFinance(request, "finance", approved: true, null, canReview: true);

        var service = new PaymentExecutionService(new ExecutionRepository(), paymentRequests, purchaseOrders,
            new SupplierRepository(supplier), new SettlementService(settlements, purchaseOrders, new SalesOrderRepository()));
        var execution = service.Register(request, "ZF-PAY-001", DateOnly.FromDateTime(DateTime.Today), OaPaymentExecutionChannel.BankTransfer,
            "BANK-001", "已核对采购订单", "finance", canRegister: true);

        Assert.Equal(OaPaymentRequestStatus.Paid, request.Status);
        Assert.NotNull(execution.ErpSettlementId);
        Assert.Equal(100m, settlements.List().Single().Amount);
        Assert.Equal(ErpSettlementStatus.Active, settlements.List().Single().Status);
        Assert.Equal(execution.Id, service.Register(request, "ZF-OTHER", DateOnly.FromDateTime(DateTime.Today), OaPaymentExecutionChannel.Cash,
            "BANK-OTHER", null, "finance", canRegister: true).Id);
    }

    [Fact]
    public void EmployeePayment_RecordsExternalReferenceWithoutFakingErpOrder()
    {
        var userId = Guid.CreateVersion7();
        var requestRepository = new PaymentRequestRepository();
        var paymentRequests = new PaymentRequestService(requestRepository);
        var request = paymentRequests.Create(userId, "申请人", "财务部", "Velrix", "FK-PAY-002", "员工甲", "末四位 5678", "工商银行", "CNY", 80,
            OaPaymentRequestType.EmployeePayment, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today), "BX-PAY-002", null, "差旅报销付款", "{}");
        paymentRequests.Submit(request, userId);
        paymentRequests.ApplyApproval(request);
        paymentRequests.ReviewFinance(request, "finance", approved: true, null, canReview: true);
        var executionRepository = new ExecutionRepository();
        var settlementRepository = new SettlementRepository();
        var service = new PaymentExecutionService(executionRepository, paymentRequests, new PurchaseOrderRepository(), new SupplierRepository(),
            new SettlementService(settlementRepository, new PurchaseOrderRepository(), new SalesOrderRepository()));

        var execution = service.Register(request, "ZF-PAY-002", DateOnly.FromDateTime(DateTime.Today), OaPaymentExecutionChannel.BankTransfer,
            "BANK-002", null, "finance", canRegister: true);

        Assert.Equal(OaPaymentRequestStatus.Paid, request.Status);
        Assert.Null(execution.ErpSettlementId);
        Assert.Empty(settlementRepository.List());
        Assert.Empty(service.ListPending([request], canRegister: true));
    }

    [Fact]
    public void RegistrationRequiresPermissionFinanceReviewAndSupplierPurchaseOrder()
    {
        var userId = Guid.CreateVersion7();
        var requestRepository = new PaymentRequestRepository();
        var paymentRequests = new PaymentRequestService(requestRepository);
        var request = paymentRequests.Create(userId, "申请人", "财务部", "Velrix", "FK-PAY-003", "供应商", "末四位 1234", "工商银行", "CNY", 10,
            OaPaymentRequestType.SupplierPayment, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today), "PO-NOT-FOUND", null, "付款", "{}");
        paymentRequests.Submit(request, userId);
        paymentRequests.ApplyApproval(request);
        var service = new PaymentExecutionService(new ExecutionRepository(), paymentRequests, new PurchaseOrderRepository(), new SupplierRepository(),
            new SettlementService(new SettlementRepository(), new PurchaseOrderRepository(), new SalesOrderRepository()));

        Assert.Throws<UnauthorizedAccessException>(() => service.Register(request, "ZF-PAY-003", DateOnly.FromDateTime(DateTime.Today), OaPaymentExecutionChannel.Other,
            "EXT-003", null, "finance", canRegister: false));
        Assert.Throws<InvalidOperationException>(() => service.Register(request, "ZF-PAY-003", DateOnly.FromDateTime(DateTime.Today), OaPaymentExecutionChannel.Other,
            "EXT-003", null, "finance", canRegister: true));
        paymentRequests.ReviewFinance(request, "finance", approved: true, null, canReview: true);
        Assert.Throws<InvalidOperationException>(() => service.Register(request, "ZF-PAY-003", DateOnly.FromDateTime(DateTime.Today), OaPaymentExecutionChannel.Other,
            "EXT-003", null, "finance", canRegister: true));
    }

    private sealed class PaymentRequestRepository : IOaPaymentRequestRepository
    {
        private readonly List<OaPaymentRequest> items = [];
        public IReadOnlyList<OaPaymentRequest> List(Guid? applicantUserId = null) => items.Where(x => applicantUserId is null || x.ApplicantUserId == applicantUserId).ToArray();
        public OaPaymentRequest? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public void Add(OaPaymentRequest item) => items.Add(item);
        public void Update(OaPaymentRequest item) { }
    }

    private sealed class ExecutionRepository : IOaPaymentExecutionRepository
    {
        private readonly List<OaPaymentExecution> items = [];
        public IReadOnlyList<OaPaymentExecution> List() => items;
        public OaPaymentExecution? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public OaPaymentExecution? GetByPaymentRequest(Guid paymentRequestId) => items.FirstOrDefault(x => x.PaymentRequestId == paymentRequestId);
        public void Add(OaPaymentExecution item) => items.Add(item);
    }

    private sealed class PurchaseOrderRepository(params PurchaseOrder[] initial) : IPurchaseOrderRepository
    {
        private readonly List<PurchaseOrder> items = [.. initial];
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

    private sealed class SettlementRepository : ISettlementRepository
    {
        private readonly List<ErpSettlement> items = [];
        public IReadOnlyList<ErpSettlement> List() => items;
        public void Add(ErpSettlement item) => items.Add(item);
        public void Update(ErpSettlement item) { }
    }

    private sealed class SalesOrderRepository : ISalesOrderRepository
    {
        public IReadOnlyList<SalesOrder> List() => [];
        public void Add(SalesOrder item) { }
        public void Update(SalesOrder item) { }
    }
}
