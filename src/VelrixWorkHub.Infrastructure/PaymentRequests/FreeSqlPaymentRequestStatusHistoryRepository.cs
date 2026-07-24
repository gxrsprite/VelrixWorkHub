using FreeSql;
using VelrixWorkHub.Application.PaymentRequests;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PaymentRequests;

public sealed class FreeSqlPaymentRequestStatusHistoryRepository(IFreeSql fsql) : IOaPaymentRequestStatusHistoryRepository
{
    public IReadOnlyList<OaPaymentRequestStatusHistory> List(Guid paymentRequestId)
        => fsql.Select<OaPaymentRequestStatusHistoryRecord>().Where(x => x.PaymentRequestId == paymentRequestId)
            .OrderByDescending(x => x.OccurredAt).ToList().OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id).Select(ToDomain).ToArray();

    public void Add(OaPaymentRequestStatusHistory item)
        => fsql.Insert(new OaPaymentRequestStatusHistoryRecord
        {
            Id = item.Id, PaymentRequestId = item.PaymentRequestId, FromStatus = item.FromStatus, ToStatus = item.ToStatus,
            Reason = item.Reason, ActorName = item.ActorName, OccurredAt = item.OccurredAt
        }).ExecuteAffrows();

    private static OaPaymentRequestStatusHistory ToDomain(OaPaymentRequestStatusHistoryRecord x)
        => OaPaymentRequestStatusHistory.Restore(x.Id, x.PaymentRequestId, x.FromStatus, x.ToStatus, x.Reason, x.ActorName, x.OccurredAt);
}
