using VelrixWorkHub.Application.PaymentRequests;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PaymentBatchServiceTests
{
    [Fact]
    public void AddsApprovedFinanceReviewedRequestsAndKeepsBatchTotals()
    {
        var repository = new BatchRepository();
        var itemRepository = new BatchItemRepository();
        var request = ApprovedRequest("FK-BATCH-001", "CNY", 120);
        var requests = new RequestRepository(request);
        var service = new PaymentBatchService(repository, itemRepository, new ExecutionRepository(), requests);
        var batch = service.Create("PAYBATCH-001", DateOnly.FromDateTime(DateTime.Today), "CNY", "finance", "{}", canManage: true);
        Assert.Single(service.ListEligible([request], canManage: true));

        var item = service.AddRequest(batch, request, canManage: true);

        Assert.Equal(batch.Id, item.BatchId);
        Assert.Equal(120m, batch.TotalAmount);
        Assert.Equal(1, batch.ItemCount);
        Assert.Empty(service.ListEligible([request], canManage: true));
        Assert.Throws<InvalidOperationException>(() => service.AddRequest(batch, request, canManage: true));
    }

    [Fact]
    public void RejectsCurrencyMismatchAndPaidRequest()
    {
        var repository = new BatchRepository();
        var itemRepository = new BatchItemRepository();
        var executions = new ExecutionRepository();
        var usdRequest = ApprovedRequest("FK-BATCH-002", "USD", 80);
        var paidRequest = ApprovedRequest("FK-BATCH-003", "CNY", 90);
        var requests = new RequestRepository(usdRequest, paidRequest);
        var service = new PaymentBatchService(repository, itemRepository, executions, requests);
        var batch = service.Create("PAYBATCH-002", DateOnly.FromDateTime(DateTime.Today), "CNY", "finance", "{}", canManage: true);
        Assert.Throws<InvalidOperationException>(() => service.AddRequest(batch, usdRequest, canManage: true));

        executions.Add(new OaPaymentExecution(paidRequest.Id, "ZF-BATCH-003", DateOnly.FromDateTime(DateTime.Today), paidRequest.Amount,
            paidRequest.Currency, OaPaymentExecutionChannel.BankTransfer, "BANK-BATCH-003", null, null, "finance", DateTime.Now));
        Assert.Throws<InvalidOperationException>(() => service.AddRequest(batch, paidRequest, canManage: true));
    }

    [Fact]
    public void SubmittedBatchCannotChangeButCancelledBatchCanBeRebatched()
    {
        var repository = new BatchRepository();
        var itemRepository = new BatchItemRepository();
        var request = ApprovedRequest("FK-BATCH-004", "CNY", 45);
        var requestRepository = new RequestRepository(request);
        var service = new PaymentBatchService(repository, itemRepository, new ExecutionRepository(), requestRepository);
        var first = service.Create("PAYBATCH-004", DateOnly.FromDateTime(DateTime.Today), "CNY", "finance", "{}", canManage: true);
        var item = service.AddRequest(first, request, canManage: true);

        request.SetStatus(OaPaymentRequestStatus.Paid);
        Assert.Throws<InvalidOperationException>(() => service.Submit(first, canManage: true));
        request.SetStatus(OaPaymentRequestStatus.Approved);
        service.Submit(first, canManage: true);
        Assert.Throws<InvalidOperationException>(() => service.RemoveRequest(first, item.Id, canManage: true));
        service.Cancel(first, canManage: true);

        var second = service.Create("PAYBATCH-005", DateOnly.FromDateTime(DateTime.Today), "CNY", "finance", "{}", canManage: true);
        service.AddRequest(second, request, canManage: true);
        Assert.Equal(45m, second.TotalAmount);
    }

    [Fact]
    public void SubmitRejectsEmptyOrInconsistentBatchTotals()
    {
        var repository = new BatchRepository();
        var itemRepository = new BatchItemRepository();
        var service = new PaymentBatchService(repository, itemRepository, new ExecutionRepository(), new RequestRepository());
        var empty = service.Create("PAYBATCH-EMPTY", DateOnly.FromDateTime(DateTime.Today), "CNY", "finance", "{}", canManage: true);

        Assert.Throws<InvalidOperationException>(() => service.Submit(empty, canManage: true));

        var request = ApprovedRequest("FK-BATCH-INCONSISTENT", "CNY", 100);
        var inconsistent = OaPaymentBatch.Restore(Guid.CreateVersion7(), "PAYBATCH-INCONSISTENT", DateOnly.FromDateTime(DateTime.Today), "CNY",
            totalAmount: 90, itemCount: 1, createdBy: "finance", otherInfo: "{}", status: OaPaymentBatchStatus.Draft, createdAt: DateTime.Now);
        repository.Add(inconsistent);
        itemRepository.Add(new OaPaymentBatchItem(inconsistent.Id, request.Id, 100, DateTime.Now));

        Assert.Throws<InvalidOperationException>(() => service.Submit(inconsistent, canManage: true));
        Assert.Equal(OaPaymentBatchStatus.Draft, inconsistent.Status);
    }

    [Fact]
    public void AllMutationsRequirePermission()
    {
        var service = new PaymentBatchService(new BatchRepository(), new BatchItemRepository(), new ExecutionRepository(), new RequestRepository());
        Assert.Throws<UnauthorizedAccessException>(() => service.Create("PAYBATCH-006", DateOnly.FromDateTime(DateTime.Today), "CNY", "finance", "{}", false));
    }

    private static OaPaymentRequest ApprovedRequest(string documentNo, string currency, decimal amount)
    {
        var item = new OaPaymentRequest(Guid.CreateVersion7(), "申请人", "财务部", "Velrix", documentNo, "收款方", "账户引用", "银行", currency,
            amount, OaPaymentRequestType.Other, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today), "业务依据", null, "付款", "{}", DateTime.Now);
        item.Submit(DateTime.Now);
        item.Approve();
        item.ReviewFinance("finance", approved: true);
        return item;
    }

    private sealed class BatchRepository : IOaPaymentBatchRepository
    {
        private readonly List<OaPaymentBatch> items = [];
        public IReadOnlyList<OaPaymentBatch> List() => items;
        public OaPaymentBatch? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public void Add(OaPaymentBatch item) => items.Add(item);
        public void Update(OaPaymentBatch item) { }
    }

    private sealed class BatchItemRepository : IOaPaymentBatchItemRepository
    {
        private readonly List<OaPaymentBatchItem> items = [];
        public IReadOnlyList<OaPaymentBatchItem> List(Guid? batchId = null) => items.Where(x => batchId is null || x.BatchId == batchId.Value).ToArray();
        public OaPaymentBatchItem? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public void Add(OaPaymentBatchItem item) => items.Add(item);
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class RequestRepository(params OaPaymentRequest[] initial) : IOaPaymentRequestRepository
    {
        private readonly List<OaPaymentRequest> items = [.. initial];
        public IReadOnlyList<OaPaymentRequest> List(Guid? applicantUserId = null) => items.Where(x => applicantUserId is null || x.ApplicantUserId == applicantUserId.Value).ToArray();
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
}
