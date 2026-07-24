using VelrixWorkHub.Application.ExpenseReimbursements;
using VelrixWorkHub.Application.PaymentRequests;
using VelrixWorkHub.Application.PurchaseOrders;
using VelrixWorkHub.Application.SalesOrders;
using VelrixWorkHub.Application.Settlements;
using VelrixWorkHub.Application.Suppliers;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class ExpenseReimbursementPaymentServiceTests
{
    [Fact]
    public void ApprovedReimbursementCreatesOneEmployeePaymentRequestAndMarksReimbursed()
    {
        var user = Guid.CreateVersion7();
        var reimbursements = new ReimbursementRepository();
        var expenseService = new ExpenseReimbursementService(reimbursements, new LineRepository());
        var paymentRequests = new PaymentRequestService(new PaymentRequestRepository());
        var service = new ExpenseReimbursementPaymentService(expenseService, paymentRequests);
        var reimbursement = CreateApproved(expenseService, user, "BX-PAYMENT-001", 180m);

        var payment = service.CreateForApprovedReimbursement(
            reimbursement.Id, "FK-BX-PAYMENT-001", "员工卡末四位 1234", "工商银行", "CNY",
            DateOnly.FromDateTime(DateTime.Today), "{}", canCreate: true);

        Assert.Equal(OaPaymentRequestType.EmployeePayment, payment.PaymentType);
        Assert.Equal(OaPaymentRequestStatus.Draft, payment.Status);
        Assert.Equal(reimbursement.DocumentNo, payment.PrecedingDocumentNo);
        Assert.Equal(reimbursement.ActualAmount, payment.Amount);
        Assert.Equal(OaExpenseReimbursementStatus.Reimbursed, reimbursement.Status);

        var retry = service.CreateForApprovedReimbursement(
            reimbursement.Id, "FK-BX-PAYMENT-RETRY", "其他账户", "其他银行", "CNY",
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)), "{}", canCreate: true);

        Assert.Equal(payment.Id, retry.Id);
        Assert.Single(paymentRequests.List());
    }

    [Fact]
    public void PaymentCreationRequiresPermissionAndApprovedReimbursement()
    {
        var user = Guid.CreateVersion7();
        var reimbursements = new ReimbursementRepository();
        var expenseService = new ExpenseReimbursementService(reimbursements, new LineRepository());
        var paymentRequests = new PaymentRequestService(new PaymentRequestRepository());
        var service = new ExpenseReimbursementPaymentService(expenseService, paymentRequests);
        var reimbursement = expenseService.Create(user, "alice", "交付部", "Velrix", "BX-PAYMENT-002", "未批准报销",
            DateOnly.FromDateTime(DateTime.Today), OaExpenseReimbursementType.General, null, false, false, false, "测试", "{}");
        expenseService.AddLine(reimbursement, user, "交通", "出租车", "INV-002", null, DateOnly.FromDateTime(DateTime.Today), 50, 50, null, "{}");

        Assert.Throws<UnauthorizedAccessException>(() => service.CreateForApprovedReimbursement(
            reimbursement.Id, "FK-BX-PAYMENT-002", "末四位 0002", "工商银行", "CNY",
            DateOnly.FromDateTime(DateTime.Today), "{}", canCreate: false));
        Assert.Throws<InvalidOperationException>(() => service.CreateForApprovedReimbursement(
            reimbursement.Id, "FK-BX-PAYMENT-002", "末四位 0002", "工商银行", "CNY",
            DateOnly.FromDateTime(DateTime.Today), "{}", canCreate: true));
    }

    [Fact]
    public void EmployeePaymentCompletionMarksLinkedReimbursementPaid()
    {
        var user = Guid.CreateVersion7();
        var reimbursementRepository = new ReimbursementRepository();
        var expenseService = new ExpenseReimbursementService(reimbursementRepository, new LineRepository());
        var reimbursement = CreateApproved(expenseService, user, "BX-PAYMENT-003", 260m);
        var paymentRequestRepository = new PaymentRequestRepository();
        var paymentRequests = new PaymentRequestService(paymentRequestRepository);
        var paymentCreation = new ExpenseReimbursementPaymentService(expenseService, paymentRequests);
        var request = paymentCreation.CreateForApprovedReimbursement(
            reimbursement.Id, "FK-BX-PAYMENT-003", "员工卡末四位 5678", "建设银行", "CNY",
            DateOnly.FromDateTime(DateTime.Today), "{}", canCreate: true);
        paymentRequests.Submit(request, user);
        paymentRequests.ApplyApproval(request);
        paymentRequests.ReviewFinance(request, "finance", approved: true, null, canReview: true);

        var execution = new PaymentExecutionService(
            new ExecutionRepository(),
            paymentRequests,
            new PurchaseOrderRepository(),
            new SupplierRepository(),
            new SettlementService(new SettlementRepository(), new PurchaseOrderRepository(), new SalesOrderRepository()),
            transactions: null,
            reimbursements: expenseService);

        execution.Register(request, "ZF-BX-PAYMENT-003", DateOnly.FromDateTime(DateTime.Today),
            OaPaymentExecutionChannel.BankTransfer, "BANK-BX-PAYMENT-003", null, "finance", canRegister: true);

        Assert.Equal(OaPaymentRequestStatus.Paid, request.Status);
        Assert.Equal(OaExpenseReimbursementStatus.Paid, reimbursement.Status);
    }

    private static OaExpenseReimbursement CreateApproved(ExpenseReimbursementService service, Guid user, string documentNo, decimal amount)
    {
        var reimbursement = service.Create(user, "alice", "交付部", "Velrix", documentNo, "客户拜访报销",
            DateOnly.FromDateTime(DateTime.Today), OaExpenseReimbursementType.Travel, null, false, false, false, "客户拜访", "{}");
        service.AddLine(reimbursement, user, "交通", "往返交通", $"INV-{documentNo}", null,
            DateOnly.FromDateTime(DateTime.Today), amount, amount, null, "{}");
        service.Submit(reimbursement, user);
        service.ApplyApproval(reimbursement);
        return reimbursement;
    }

    private sealed class ReimbursementRepository : IOaExpenseReimbursementRepository
    {
        private readonly List<OaExpenseReimbursement> items = [];
        public IReadOnlyList<OaExpenseReimbursement> List(Guid? applicantUserId = null) => items.Where(x => applicantUserId is null || x.ApplicantUserId == applicantUserId).ToArray();
        public OaExpenseReimbursement? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public void Add(OaExpenseReimbursement item) => items.Add(item);
        public void Update(OaExpenseReimbursement item) { }
    }

    private sealed class LineRepository : IOaExpenseLineRepository
    {
        private readonly List<OaExpenseLine> items = [];
        public IReadOnlyList<OaExpenseLine> List(Guid? reimbursementId = null) => items.Where(x => reimbursementId is null || x.ReimbursementId == reimbursementId).ToArray();
        public OaExpenseLine? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public void Add(OaExpenseLine item) => items.Add(item);
        public void Update(OaExpenseLine item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
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

    private sealed class PurchaseOrderRepository : IPurchaseOrderRepository
    {
        public IReadOnlyList<PurchaseOrder> List() => [];
        public void Add(PurchaseOrder item) { }
        public void Update(PurchaseOrder item) { }
    }

    private sealed class SupplierRepository : ISupplierRepository
    {
        public IReadOnlyList<Supplier> List() => [];
        public void Add(Supplier item) { }
        public void Update(Supplier item) { }
        public void Remove(Guid id) { }
    }

    private sealed class SettlementRepository : ISettlementRepository
    {
        public IReadOnlyList<ErpSettlement> List() => [];
        public void Add(ErpSettlement item) { }
        public void Update(ErpSettlement item) { }
    }

    private sealed class SalesOrderRepository : ISalesOrderRepository
    {
        public IReadOnlyList<SalesOrder> List() => [];
        public void Add(SalesOrder item) { }
        public void Update(SalesOrder item) { }
    }
}
