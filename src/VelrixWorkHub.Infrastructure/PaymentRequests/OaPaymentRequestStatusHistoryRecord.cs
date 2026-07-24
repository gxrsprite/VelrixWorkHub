using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PaymentRequests;

[Table(Name = "OaPaymentRequestStatusHistory")]
[Index("OaPaymentRequestStatusHistory_ix_RequestOccurredAt", nameof(PaymentRequestId) + "," + nameof(OccurredAt))]
public sealed class OaPaymentRequestStatusHistoryRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid PaymentRequestId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 3)] public OaPaymentRequestStatus FromStatus { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 4)] public OaPaymentRequestStatus ToStatus { get; set; }
    [Column(StringLength = 1000, IsNullable = true, Position = 5)] public string? Reason { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 6)] public string ActorName { get; set; } = string.Empty;
    [Column(IsNullable = false, ServerTime = DateTimeKind.Local, Position = 7)] public DateTime OccurredAt { get; set; }
}
