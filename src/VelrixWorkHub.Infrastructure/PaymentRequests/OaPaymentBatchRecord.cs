using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PaymentRequests;

[Table(Name = "OaPaymentBatch")]
[Index("OaPaymentBatch_uk_BatchNo", nameof(BatchNo), true)]
public sealed class OaPaymentBatchRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 2)] public string BatchNo { get; set; } = string.Empty;
    [Column(IsNullable = false, Position = 3)] public DateTime PaymentDate { get; set; }
    [Column(StringLength = 10, IsNullable = false, Position = 4)] public string Currency { get; set; } = "CNY";
    [Column(DbType = "numeric(18,2)", IsNullable = false, Position = 5)] public decimal TotalAmount { get; set; }
    [Column(IsNullable = false, Position = 6)] public int ItemCount { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 7)] public string CreatedBy { get; set; } = string.Empty;
    [Column(StringLength = 4000, IsNullable = false, Position = 8)] public string OtherInfo { get; set; } = "{}";
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 9)] public OaPaymentBatchStatus Status { get; set; }
    [Column(IsNullable = false, ServerTime = DateTimeKind.Local, Position = 10)] public DateTime CreatedAt { get; set; }
}

[Table(Name = "OaPaymentBatchItem")]
[Index("OaPaymentBatchItem_ix_BatchId", nameof(BatchId))]
[Index("OaPaymentBatchItem_ix_PaymentRequestId", nameof(PaymentRequestId))]
public sealed class OaPaymentBatchItemRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid BatchId { get; set; }
    [Column(IsNullable = false, Position = 3)] public Guid PaymentRequestId { get; set; }
    [Column(DbType = "numeric(18,2)", IsNullable = false, Position = 4)] public decimal Amount { get; set; }
    [Column(IsNullable = false, ServerTime = DateTimeKind.Local, Position = 5)] public DateTime CreatedAt { get; set; }
}
