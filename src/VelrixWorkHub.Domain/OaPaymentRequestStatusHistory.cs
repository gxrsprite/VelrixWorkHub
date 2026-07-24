namespace VelrixWorkHub.Domain;

/// <summary>付款申请生命周期的不可变状态历史。</summary>
public sealed class OaPaymentRequestStatusHistory
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid PaymentRequestId { get; private set; }
    public OaPaymentRequestStatus FromStatus { get; private set; }
    public OaPaymentRequestStatus ToStatus { get; private set; }
    public string? Reason { get; private set; }
    public string ActorName { get; private set; } = string.Empty;
    public DateTime OccurredAt { get; private set; }

    public OaPaymentRequestStatusHistory(Guid paymentRequestId, OaPaymentRequestStatus fromStatus, OaPaymentRequestStatus toStatus,
        string? reason, string actorName, DateTime occurredAt)
    {
        if (paymentRequestId == Guid.Empty) throw new ArgumentException("付款申请不能为空。", nameof(paymentRequestId));
        if (fromStatus == toStatus) throw new ArgumentException("付款申请状态没有发生变化。", nameof(toStatus));
        PaymentRequestId = paymentRequestId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        ActorName = string.IsNullOrWhiteSpace(actorName) ? "system" : actorName.Trim();
        OccurredAt = occurredAt;
    }

    public static OaPaymentRequestStatusHistory Restore(Guid id, Guid paymentRequestId, OaPaymentRequestStatus fromStatus,
        OaPaymentRequestStatus toStatus, string? reason, string actorName, DateTime occurredAt)
    {
        var item = new OaPaymentRequestStatusHistory(paymentRequestId, fromStatus, toStatus, reason, actorName, occurredAt);
        item.Id = id;
        return item;
    }
}
