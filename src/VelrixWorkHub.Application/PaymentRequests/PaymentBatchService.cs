using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PaymentRequests;

public interface IOaPaymentBatchRepository
{
    IReadOnlyList<OaPaymentBatch> List();
    OaPaymentBatch? Get(Guid id);
    void Add(OaPaymentBatch item);
    void Update(OaPaymentBatch item);
}

public interface IOaPaymentBatchItemRepository
{
    IReadOnlyList<OaPaymentBatchItem> List(Guid? batchId = null);
    OaPaymentBatchItem? Get(Guid id);
    void Add(OaPaymentBatchItem item);
    void Remove(Guid id);
}

public sealed class PaymentBatchService(
    IOaPaymentBatchRepository batches,
    IOaPaymentBatchItemRepository items,
    IOaPaymentExecutionRepository executions,
    IOaPaymentRequestRepository paymentRequests)
{
    public IReadOnlyList<OaPaymentBatch> List()
        => batches.List().OrderByDescending(x => x.PaymentDate).ThenByDescending(x => x.CreatedAt).ToArray();

    public OaPaymentBatch? Get(Guid id) => batches.Get(id);

    public IReadOnlyList<OaPaymentBatchItem> ListItems(Guid batchId)
        => items.List(batchId).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToArray();

    public OaPaymentBatchItem? GetByPaymentRequest(Guid paymentRequestId)
        => items.List().Where(x => x.PaymentRequestId == paymentRequestId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefault();

    public OaPaymentBatch Create(string batchNo, DateOnly paymentDate, string currency, string createdBy, string? otherInfo, bool canManage)
    {
        EnsureManagePermission(canManage);
        if (batches.List().Any(x => x.BatchNo.Equals(batchNo.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("付款批次号已存在。");
        var item = new OaPaymentBatch(batchNo, paymentDate, currency, createdBy, otherInfo, DateTime.Now);
        batches.Add(item);
        return item;
    }

    public IReadOnlyList<OaPaymentRequest> ListEligible(IEnumerable<OaPaymentRequest> requests, bool canManage)
    {
        EnsureManagePermission(canManage);
        var activeRequestIds = items.List().Where(x => batches.Get(x.BatchId)?.Status != OaPaymentBatchStatus.Cancelled)
            .Select(x => x.PaymentRequestId).ToHashSet();
        var executedRequestIds = executions.List().Select(x => x.PaymentRequestId).ToHashSet();
        return requests.Where(x => x.Status == OaPaymentRequestStatus.Approved
                && x.FinanceReviewStatus == OaPaymentFinanceReviewStatus.Approved
                && !executedRequestIds.Contains(x.Id)
                && !activeRequestIds.Contains(x.Id))
            .OrderBy(x => x.RequestedPaymentDate).ThenBy(x => x.CreatedAt).ToArray();
    }

    public OaPaymentBatchItem AddRequest(OaPaymentBatch batch, OaPaymentRequest request, bool canManage)
    {
        EnsureManagePermission(canManage);
        EnsureBatch(batch, OaPaymentBatchStatus.Draft);
        if (request.Status != OaPaymentRequestStatus.Approved || request.FinanceReviewStatus != OaPaymentFinanceReviewStatus.Approved)
            throw new InvalidOperationException("只有财务复核通过的已批准付款申请才能加入付款批次。");
        if (executions.GetByPaymentRequest(request.Id) is not null)
            throw new InvalidOperationException("已有实际付款记录的申请不能加入付款批次。");
        var existing = GetActiveItem(request.Id);
        if (existing is not null) throw new InvalidOperationException("付款申请已经加入其他未撤回的付款批次。");
        if (!batch.Currency.Equals(request.Currency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("付款申请币种与批次币种不一致。");

        var item = new OaPaymentBatchItem(batch.Id, request.Id, request.Amount, DateTime.Now);
        batch.Add(item.Amount);
        batches.Update(batch);
        items.Add(item);
        return item;
    }

    public void RemoveRequest(OaPaymentBatch batch, Guid itemId, bool canManage)
    {
        EnsureManagePermission(canManage);
        EnsureBatch(batch, OaPaymentBatchStatus.Draft);
        var item = items.Get(itemId) ?? throw new InvalidOperationException("付款批次明细不存在。");
        if (item.BatchId != batch.Id) throw new InvalidOperationException("付款批次明细不属于当前批次。");
        batch.Remove(item.Amount);
        batches.Update(batch);
        items.Remove(item.Id);
    }

    public void Submit(OaPaymentBatch batch, bool canManage)
    {
        EnsureManagePermission(canManage);
        EnsureBatch(batch, OaPaymentBatchStatus.Draft);
        var batchItems = items.List(batch.Id);
        if (batchItems.Count != batch.ItemCount || batchItems.Sum(x => x.Amount) != batch.TotalAmount)
            throw new InvalidOperationException("付款批次汇总与明细不一致，不能提交。");
        foreach (var item in batchItems)
        {
            var request = paymentRequests.Get(item.PaymentRequestId) ?? throw new InvalidOperationException("付款批次包含不存在的付款申请。");
            if (request.Status != OaPaymentRequestStatus.Approved || request.FinanceReviewStatus != OaPaymentFinanceReviewStatus.Approved)
                throw new InvalidOperationException($"付款申请 {request.DocumentNo} 已不满足批次提交条件。");
            if (executions.GetByPaymentRequest(request.Id) is not null)
                throw new InvalidOperationException($"付款申请 {request.DocumentNo} 已登记实际付款，不能提交批次。");
            if (request.Amount != item.Amount || !request.Currency.Equals(batch.Currency, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"付款申请 {request.DocumentNo} 的金额或币种已变化，不能提交批次。");
        }
        batch.Submit();
        batches.Update(batch);
    }

    public void Cancel(OaPaymentBatch batch, bool canManage)
    {
        EnsureManagePermission(canManage);
        batch.Cancel();
        batches.Update(batch);
    }

    private OaPaymentBatchItem? GetActiveItem(Guid paymentRequestId)
        => items.List().Where(x => x.PaymentRequestId == paymentRequestId)
            .Where(x => batches.Get(x.BatchId)?.Status != OaPaymentBatchStatus.Cancelled)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefault();

    private static void EnsureBatch(OaPaymentBatch batch, OaPaymentBatchStatus expected)
    {
        if (batch.Status != expected) throw new InvalidOperationException("当前付款批次状态不允许此操作。");
    }

    private static void EnsureManagePermission(bool canManage)
    {
        if (!canManage) throw new UnauthorizedAccessException("当前用户没有维护付款批次的权限。");
    }
}
