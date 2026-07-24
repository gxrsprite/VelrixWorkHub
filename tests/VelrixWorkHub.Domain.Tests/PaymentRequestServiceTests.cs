using VelrixWorkHub.Application.PaymentRequests;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PaymentRequestServiceTests
{
    [Fact]
    public void SubmitRequiresPrecedingDocumentAndObjectOtherInfo()
    {
        var repository = new PaymentRequestRepository();
        var service = new PaymentRequestService(repository);
        var user = Guid.CreateVersion7();
        var item = service.Create(user, "alice", "财务部", "Velrix", "FK-001", "测试供应商", "末四位 1234", "中国工商银行", "CNY", 100, OaPaymentRequestType.SupplierPayment, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(7)), null, null, "测试采购付款", "{}");

        Assert.Throws<InvalidOperationException>(() => service.Submit(item, user));
        Assert.Throws<ArgumentException>(() => service.Create(user, "alice", "财务部", "Velrix", "FK-002", "供应商", "末四位 1234", "工商银行", "CNY", 100, OaPaymentRequestType.SupplierPayment, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today), "PO-002", null, "采购付款", "[]"));
        Assert.Equal(OaPaymentRequestStatus.Draft, item.Status);
    }

    [Fact]
    public void ApplicantIsolationAndDuplicateDocumentNumberAreEnforced()
    {
        var repository = new PaymentRequestRepository();
        var service = new PaymentRequestService(repository);
        var user = Guid.CreateVersion7();
        var otherUser = Guid.CreateVersion7();
        var item = Create(service, user, "FK-003", "{}");

        Assert.Single(service.ListMine(user));
        Assert.Empty(service.ListMine(otherUser));
        Assert.Throws<UnauthorizedAccessException>(() => service.Edit(item, otherUser, "other", "财务部", "Velrix", item.DocumentNo, "供应商", "末四位 1234", "工商银行", "CNY", 100, OaPaymentRequestType.SupplierPayment, item.RequestDate, item.RequestedPaymentDate, "PO-003", null, "采购付款", "{}"));
        Assert.Throws<InvalidOperationException>(() => service.Create(user, "alice", "财务部", "Velrix", "fk-003", "另一个供应商", "末四位 5678", "工商银行", "CNY", 50, OaPaymentRequestType.SupplierPayment, item.RequestDate, item.RequestedPaymentDate, "PO-004", null, "采购付款", "{}"));
    }

    [Fact]
    public void RejectedRequestCanBeEditedAndResubmitted()
    {
        var repository = new PaymentRequestRepository();
        var service = new PaymentRequestService(repository);
        var user = Guid.CreateVersion7();
        var item = Create(service, user, "FK-005", "{\"costCenter\":\"C-01\"}");

        item.Submit(DateTime.Now);
        item.Reject("请补充合同依据");
        repository.Update(item);
        service.Edit(item, user, "alice", "财务部", "Velrix", item.DocumentNo, item.PayeeName, item.PayeeAccountReference, item.PaymentBankName, item.Currency, 120, item.PaymentType, item.RequestDate, item.RequestedPaymentDate, "合同-005", null, "补充供应商合同付款", item.OtherInfo);
        service.Submit(item, user);

        Assert.Equal(OaPaymentRequestStatus.Submitted, item.Status);
        Assert.Equal(120, item.Amount);
        Assert.Equal("合同-005", item.PrecedingDocumentNo);
        Assert.Null(item.RejectionReason);
    }

    [Fact]
    public void DateCurrencyAndAmountRulesAreEnforced()
    {
        var user = Guid.CreateVersion7();
        var today = DateOnly.FromDateTime(DateTime.Today);
        Assert.Throws<ArgumentException>(() => new OaPaymentRequest(user, "alice", "财务部", "Velrix", "FK-006", "供应商", "末四位 1234", "工商银行", "C", 1, OaPaymentRequestType.SupplierPayment, today, today, "PO-006", null, "付款", "{}", DateTime.Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OaPaymentRequest(user, "alice", "财务部", "Velrix", "FK-007", "供应商", "末四位 1234", "工商银行", "CNY", 0, OaPaymentRequestType.SupplierPayment, today, today, "PO-007", null, "付款", "{}", DateTime.Now));
        Assert.Throws<ArgumentException>(() => new OaPaymentRequest(user, "alice", "财务部", "Velrix", "FK-008", "供应商", "末四位 1234", "工商银行", "CNY", 1, OaPaymentRequestType.SupplierPayment, today, today.AddDays(-1), "PO-008", null, "付款", "{}", DateTime.Now));
    }

    [Fact]
    public void PaidTransitionIsOnlyAllowedAfterApproval()
    {
        var service = new PaymentRequestService(new PaymentRequestRepository());
        var item = Create(service, Guid.CreateVersion7(), "FK-009", "{}");
        Assert.Throws<InvalidOperationException>(item.MarkPaid);
        item.Submit(DateTime.Now);
        item.Approve();
        Assert.Throws<InvalidOperationException>(item.MarkPaid);
        item.ReviewFinance("finance", approved: true);
        item.MarkPaid();
        Assert.Equal(OaPaymentRequestStatus.Paid, item.Status);
    }

    [Fact]
    public void FinanceReviewIsRequiredAfterWorkflowApprovalAndRejectNeedsReason()
    {
        var repository = new PaymentRequestRepository();
        var service = new PaymentRequestService(repository);
        var item = Create(service, Guid.CreateVersion7(), "FK-010", "{}");

        Assert.Empty(service.ListPendingFinanceReview());
        item.Submit(DateTime.Now);
        item.Approve();
        repository.Update(item);

        Assert.Single(service.ListPendingFinanceReview());
        Assert.Throws<UnauthorizedAccessException>(() => service.ReviewFinance(item, "finance", approved: false, null, canReview: false));
        Assert.Throws<ArgumentException>(() => service.ReviewFinance(item, "finance", approved: false, null, canReview: true));
        service.ReviewFinance(item, "finance", approved: false, "收款依据不完整", canReview: true);

        Assert.Equal(OaPaymentRequestStatus.Rejected, item.Status);
        Assert.Equal(OaPaymentFinanceReviewStatus.Rejected, item.FinanceReviewStatus);
        Assert.Equal("收款依据不完整", item.FinanceReviewReason);
        Assert.Empty(service.ListPendingFinanceReview());
        service.Edit(item, item.ApplicantUserId, "alice", "财务部", "Velrix", item.DocumentNo, item.PayeeName, item.PayeeAccountReference,
            item.PaymentBankName, item.Currency, item.Amount, item.PaymentType, item.RequestDate, item.RequestedPaymentDate,
            item.PrecedingDocumentNo, item.ProjectId, "补充收款依据", item.OtherInfo);
        service.Submit(item, item.ApplicantUserId);
        Assert.Equal(OaPaymentRequestStatus.Submitted, item.Status);
        Assert.Equal(OaPaymentFinanceReviewStatus.Pending, item.FinanceReviewStatus);
    }

    private static OaPaymentRequest Create(PaymentRequestService service, Guid user, string documentNo, string otherInfo)
        => service.Create(user, "alice", "财务部", "Velrix 上海有限公司", documentNo, "测试供应商", "末四位 1234", "中国工商银行", "cny", 100, OaPaymentRequestType.SupplierPayment, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(7)), "PO-" + documentNo[3..], null, "测试采购付款", otherInfo);

    private sealed class PaymentRequestRepository : IOaPaymentRequestRepository
    {
        private readonly List<OaPaymentRequest> items = [];
        public IReadOnlyList<OaPaymentRequest> List(Guid? applicantUserId = null) => items.Where(x => applicantUserId is null || x.ApplicantUserId == applicantUserId).ToArray();
        public OaPaymentRequest? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public void Add(OaPaymentRequest item) => items.Add(item);
        public void Update(OaPaymentRequest item) { if (!items.Contains(item)) throw new InvalidOperationException(); }
    }
}
