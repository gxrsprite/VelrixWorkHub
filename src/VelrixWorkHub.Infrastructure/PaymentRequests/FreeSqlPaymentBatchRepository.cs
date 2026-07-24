using FreeSql;
using VelrixWorkHub.Application.PaymentRequests;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PaymentRequests;

public sealed class FreeSqlPaymentBatchRepository(IFreeSql fsql) : IOaPaymentBatchRepository
{
    public IReadOnlyList<OaPaymentBatch> List() => fsql.Select<OaPaymentBatchRecord>().ToList().Select(ToDomain).ToArray();
    public OaPaymentBatch? Get(Guid id) => fsql.Select<OaPaymentBatchRecord>().Where(x => x.Id == id).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(OaPaymentBatch item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(OaPaymentBatch item) => fsql.Update<OaPaymentBatchRecord>().SetSource(ToRecord(item)).ExecuteAffrows();

    private static OaPaymentBatch ToDomain(OaPaymentBatchRecord x) => OaPaymentBatch.Restore(x.Id, x.BatchNo, DateOnly.FromDateTime(x.PaymentDate),
        x.Currency, x.TotalAmount, x.ItemCount, x.CreatedBy, x.OtherInfo, x.Status, x.CreatedAt);

    private static OaPaymentBatchRecord ToRecord(OaPaymentBatch x) => new()
    {
        Id = x.Id, BatchNo = x.BatchNo, PaymentDate = x.PaymentDate.ToDateTime(TimeOnly.MinValue), Currency = x.Currency,
        TotalAmount = x.TotalAmount, ItemCount = x.ItemCount, CreatedBy = x.CreatedBy, OtherInfo = x.OtherInfo,
        Status = x.Status, CreatedAt = x.CreatedAt
    };
}

public sealed class FreeSqlPaymentBatchItemRepository(IFreeSql fsql) : IOaPaymentBatchItemRepository
{
    public IReadOnlyList<OaPaymentBatchItem> List(Guid? batchId = null)
    {
        var query = fsql.Select<OaPaymentBatchItemRecord>();
        if (batchId is not null) query = query.Where(x => x.BatchId == batchId.Value);
        return query.ToList().Select(ToDomain).ToArray();
    }

    public OaPaymentBatchItem? Get(Guid id) => fsql.Select<OaPaymentBatchItemRecord>().Where(x => x.Id == id).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(OaPaymentBatchItem item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Remove(Guid id) => fsql.Delete<OaPaymentBatchItemRecord>().Where(x => x.Id == id).ExecuteAffrows();

    private static OaPaymentBatchItem ToDomain(OaPaymentBatchItemRecord x) => OaPaymentBatchItem.Restore(x.Id, x.BatchId, x.PaymentRequestId, x.Amount, x.CreatedAt);
    private static OaPaymentBatchItemRecord ToRecord(OaPaymentBatchItem x) => new()
    {
        Id = x.Id, BatchId = x.BatchId, PaymentRequestId = x.PaymentRequestId, Amount = x.Amount, CreatedAt = x.CreatedAt
    };
}
