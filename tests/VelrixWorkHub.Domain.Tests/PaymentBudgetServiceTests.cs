using VelrixWorkHub.Application.PaymentRequests;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PaymentBudgetServiceTests
{
    [Fact]
    public void SubmitReserves_RejectionReleases_AndPaymentConsumesBudget()
    {
        var budgetRepository = new BudgetRepository();
        var reservationRepository = new ReservationRepository();
        var budgets = new PaymentBudgetService(budgetRepository, reservationRepository);
        var budget = budgets.Create("BUDGET-001", "Velrix", "财务部", "CNY", 150, "{}", canManage: true);
        var requests = new PaymentRepository();
        var paymentRequests = new PaymentRequestService(requests, budgets: budgets);
        var userId = Guid.CreateVersion7();
        var request = paymentRequests.Create(userId, "申请人", "财务部", "Velrix", "FK-BUDGET-001", "供应商", "末四位 1234", "工商银行", "CNY", 100,
            OaPaymentRequestType.SupplierPayment, Today, Today, "PO-BUDGET-001", null, "采购付款", "{}", budget.BudgetNo);

        paymentRequests.Submit(request, userId);
        Assert.Equal(100m, budget.ReservedAmount);
        Assert.Equal(50m, budget.AvailableAmount);
        Assert.Equal(OaPaymentBudgetReservationStatus.Reserved, reservationRepository.GetByPaymentRequest(request.Id)!.Status);

        paymentRequests.ApplyRejection(request, "预算依据不清", "reviewer");
        Assert.Equal(0m, budget.ReservedAmount);
        Assert.Equal(OaPaymentBudgetReservationStatus.Released, reservationRepository.GetByPaymentRequest(request.Id)!.Status);

        paymentRequests.Submit(request, userId);
        paymentRequests.ApplyApproval(request, "approver");
        paymentRequests.ReviewFinance(request, "finance", approved: true, null, canReview: true);
        paymentRequests.MarkPaid(request, "cashier");

        Assert.Equal(0m, budget.ReservedAmount);
        Assert.Equal(100m, budget.ConsumedAmount);
        Assert.Equal(50m, budget.AvailableAmount);
        Assert.Equal(OaPaymentBudgetReservationStatus.Consumed, reservationRepository.GetByPaymentRequest(request.Id)!.Status);
    }

    [Fact]
    public void SubmitRejectsInsufficientOrMismatchedBudgetAndCancelReleasesReservation()
    {
        var budgetRepository = new BudgetRepository();
        var reservationRepository = new ReservationRepository();
        var budgets = new PaymentBudgetService(budgetRepository, reservationRepository);
        budgets.Create("BUDGET-002", "Velrix", "财务部", "CNY", 50, "{}", canManage: true);
        var requests = new PaymentRepository();
        var paymentRequests = new PaymentRequestService(requests, budgets: budgets);
        var userId = Guid.CreateVersion7();
        var tooLarge = paymentRequests.Create(userId, "申请人", "财务部", "Velrix", "FK-BUDGET-002", "供应商", "末四位 1234", "工商银行", "CNY", 60,
            OaPaymentRequestType.SupplierPayment, Today, Today, "PO-BUDGET-002", null, "采购付款", "{}", "BUDGET-002");
        Assert.Throws<InvalidOperationException>(() => paymentRequests.Submit(tooLarge, userId));
        Assert.Equal(OaPaymentRequestStatus.Draft, tooLarge.Status);

        var mismatched = paymentRequests.Create(userId, "申请人", "采购部", "Velrix", "FK-BUDGET-003", "供应商", "末四位 1234", "工商银行", "CNY", 10,
            OaPaymentRequestType.SupplierPayment, Today, Today, "PO-BUDGET-003", null, "采购付款", "{}", "BUDGET-002");
        Assert.Throws<InvalidOperationException>(() => paymentRequests.Submit(mismatched, userId));
        Assert.Equal(0m, budgetRepository.List().Single().ReservedAmount);

        var valid = paymentRequests.Create(userId, "申请人", "财务部", "Velrix", "FK-BUDGET-004", "供应商", "末四位 1234", "工商银行", "CNY", 10,
            OaPaymentRequestType.SupplierPayment, Today, Today, "PO-BUDGET-004", null, "采购付款", "{}", "BUDGET-002");
        paymentRequests.Submit(valid, userId);
        paymentRequests.Cancel(valid, userId, "申请人");
        Assert.Equal(0m, budgetRepository.List().Single().ReservedAmount);
        Assert.Equal(OaPaymentBudgetReservationStatus.Released, reservationRepository.GetByPaymentRequest(valid.Id)!.Status);
    }

    [Fact]
    public void BudgetCloseRequiresNoOutstandingReservationAndPermission()
    {
        var budgetRepository = new BudgetRepository();
        var reservationRepository = new ReservationRepository();
        var budgets = new PaymentBudgetService(budgetRepository, reservationRepository);
        var budget = budgets.Create("BUDGET-003", "Velrix", "财务部", "CNY", 100, "{}", canManage: true);
        var reservation = new OaPaymentBudgetReservation(budget.Id, Guid.CreateVersion7(), 20, DateTime.Now);
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
        Assert.Equal(OaPaymentBudgetStatus.Closed, budget.Status);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    private sealed class PaymentRepository : IOaPaymentRequestRepository
    {
        private readonly List<OaPaymentRequest> items = [];
        public IReadOnlyList<OaPaymentRequest> List(Guid? applicantUserId = null) => items.Where(x => applicantUserId is null || x.ApplicantUserId == applicantUserId).ToArray();
        public OaPaymentRequest? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public void Add(OaPaymentRequest item) => items.Add(item);
        public void Update(OaPaymentRequest item) { }
    }

    private sealed class BudgetRepository : IOaPaymentBudgetRepository
    {
        private readonly List<OaPaymentBudget> items = [];
        public IReadOnlyList<OaPaymentBudget> List() => items;
        public OaPaymentBudget? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public void Add(OaPaymentBudget item) => items.Add(item);
        public void Update(OaPaymentBudget item) { }
    }

    private sealed class ReservationRepository : IOaPaymentBudgetReservationRepository
    {
        private readonly List<OaPaymentBudgetReservation> items = [];
        public IReadOnlyList<OaPaymentBudgetReservation> List(Guid? budgetId = null) => items.Where(x => budgetId is null || x.BudgetId == budgetId).ToArray();
        public OaPaymentBudgetReservation? GetByPaymentRequest(Guid paymentRequestId) => items.FirstOrDefault(x => x.PaymentRequestId == paymentRequestId);
        public void Add(OaPaymentBudgetReservation item) => items.Add(item);
        public void Update(OaPaymentBudgetReservation item) { }
    }
}
