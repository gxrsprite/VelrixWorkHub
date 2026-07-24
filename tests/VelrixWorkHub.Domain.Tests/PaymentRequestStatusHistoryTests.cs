using VelrixWorkHub.Application.PaymentRequests;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PaymentRequestStatusHistoryTests
{
    [Fact]
    public void LifecycleTransitionsAreRecordedWithoutAllowingSameStatusEvents()
    {
        var userId = Guid.CreateVersion7();
        var requests = new PaymentRepository();
        var histories = new HistoryRepository();
        var service = new PaymentRequestService(requests, statusHistory: histories);
        var item = service.Create(userId, "申请人", "财务部", "Velrix", "FK-HISTORY-001", "供应商", "末四位 1234", "工商银行", "CNY", 100,
            OaPaymentRequestType.SupplierPayment, Today, Today, "PO-HISTORY-001", null, "采购付款", "{}");

        service.Submit(item, userId);
        service.ApplyRejection(item, "补充付款依据", "reviewer");
        service.Submit(item, userId);
        service.ApplyApproval(item, "approver");
        service.ReviewFinance(item, "finance", approved: true, null, canReview: true);
        service.MarkPaid(item, "cashier");

        Assert.Equal(OaPaymentRequestStatus.Paid, item.Status);
        Assert.Equal(
            [
                (OaPaymentRequestStatus.Draft, OaPaymentRequestStatus.Submitted, userId.ToString()),
                (OaPaymentRequestStatus.Submitted, OaPaymentRequestStatus.Rejected, "reviewer"),
                (OaPaymentRequestStatus.Rejected, OaPaymentRequestStatus.Submitted, userId.ToString()),
                (OaPaymentRequestStatus.Submitted, OaPaymentRequestStatus.Approved, "approver"),
                (OaPaymentRequestStatus.Approved, OaPaymentRequestStatus.Paid, "cashier")
            ],
            histories.Items.Select(x => (x.FromStatus, x.ToStatus, x.ActorName)).ToArray());
        Assert.Equal(5, service.ListHistory(item.Id).Count);
    }

    [Fact]
    public void CancelIsRecordedAndWorkflowRejectionUsesActorFromContext()
    {
        var userId = Guid.CreateVersion7();
        var requests = new PaymentRepository();
        var histories = new HistoryRepository();
        var service = new PaymentRequestService(requests, statusHistory: histories);
        var item = service.Create(userId, "申请人", "财务部", "Velrix", "FK-HISTORY-002", "供应商", "末四位 1234", "工商银行", "CNY", 100,
            OaPaymentRequestType.SupplierPayment, Today, Today, "PO-HISTORY-002", null, "采购付款", "{}");

        service.Cancel(item, userId, "applicant");

        Assert.Equal(OaPaymentRequestStatus.Cancelled, item.Status);
        var history = Assert.Single(histories.Items);
        Assert.Equal(OaPaymentRequestStatus.Draft, history.FromStatus);
        Assert.Equal(OaPaymentRequestStatus.Cancelled, history.ToStatus);
        Assert.Equal("applicant", history.ActorName);
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

    private sealed class HistoryRepository : IOaPaymentRequestStatusHistoryRepository
    {
        public List<OaPaymentRequestStatusHistory> Items { get; } = [];
        public IReadOnlyList<OaPaymentRequestStatusHistory> List(Guid paymentRequestId) => Items.Where(x => x.PaymentRequestId == paymentRequestId).ToArray();
        public void Add(OaPaymentRequestStatusHistory item) => Items.Add(item);
    }
}
