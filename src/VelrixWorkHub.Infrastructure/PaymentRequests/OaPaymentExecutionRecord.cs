using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PaymentRequests;

[Table(Name = "OaPaymentExecution")]
[Index("OaPaymentExecution_uk_ExecutionNo", nameof(ExecutionNo), true)]
[Index("OaPaymentExecution_uk_PaymentRequestId", nameof(PaymentRequestId), true)]
public sealed class OaPaymentExecutionRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid PaymentRequestId { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 3)] public string ExecutionNo { get; set; } = string.Empty;
    [Column(IsNullable = false, Position = 4)] public DateTime PaidOn { get; set; }
    [Column(DbType = "numeric(18,2)", IsNullable = false, Position = 5)] public decimal Amount { get; set; }
    [Column(StringLength = 10, IsNullable = false, Position = 6)] public string Currency { get; set; } = "CNY";
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 7)] public OaPaymentExecutionChannel Channel { get; set; }
    [Column(StringLength = 200, IsNullable = false, Position = 8)] public string ExternalReference { get; set; } = string.Empty;
    [Column(StringLength = 2000, IsNullable = true, Position = 9)] public string? Notes { get; set; }
    [Column(IsNullable = true, Position = 10)] public Guid? ErpSettlementId { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 11)] public string Operator { get; set; } = string.Empty;
    [Column(IsNullable = false, ServerTime = DateTimeKind.Local, Position = 12)] public DateTime CreatedAt { get; set; }
}
