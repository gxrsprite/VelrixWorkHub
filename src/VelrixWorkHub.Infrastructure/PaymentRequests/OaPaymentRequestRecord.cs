using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PaymentRequests;

[Table(Name = "OaPaymentRequest")]
[Index("OaPaymentRequest_uk_DocumentNo", nameof(DocumentNo), true)]
public sealed class OaPaymentRequestRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 2)] public string DocumentNo { get; set; } = string.Empty;
    [Column(IsNullable = false, Position = 3)] public Guid ApplicantUserId { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 4)] public string ApplicantName { get; set; } = string.Empty;
    [Column(StringLength = 100, IsNullable = false, Position = 5)] public string DepartmentName { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 6)] public string LegalEntity { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 7)] public string PayeeName { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 8)] public string PayeeAccountReference { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 9)] public string PaymentBankName { get; set; } = string.Empty;
    [Column(StringLength = 10, IsNullable = false, Position = 10)] public string Currency { get; set; } = "CNY";
    [Column(DbType = "numeric(18,2)", IsNullable = false, Position = 11)] public decimal Amount { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 12)] public OaPaymentRequestType PaymentType { get; set; }
    [Column(IsNullable = false, Position = 13)] public DateTime RequestDate { get; set; }
    [Column(IsNullable = false, Position = 14)] public DateTime RequestedPaymentDate { get; set; }
    [Column(StringLength = 200, IsNullable = true, Position = 15)] public string? PrecedingDocumentNo { get; set; }
    [Column(IsNullable = true, Position = 16)] public Guid? ProjectId { get; set; }
    [Column(StringLength = 2000, IsNullable = false, Position = 17)] public string Purpose { get; set; } = string.Empty;
    [Column(StringLength = -1, IsNullable = false, Position = 18)] public string OtherInfo { get; set; } = "{}";
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 19)] public OaPaymentRequestStatus Status { get; set; }
    [Column(StringLength = 1000, IsNullable = true, Position = 20)] public string? RejectionReason { get; set; }
    [Column(IsNullable = false, ServerTime = DateTimeKind.Local, Position = 21)] public DateTime CreatedAt { get; set; }
    [Column(IsNullable = true, Position = 22)] public DateTime? SubmittedAt { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 23)] public OaPaymentFinanceReviewStatus FinanceReviewStatus { get; set; }
    [Column(StringLength = 1000, IsNullable = true, Position = 24)] public string? FinanceReviewReason { get; set; }
    [Column(StringLength = 100, IsNullable = true, Position = 25)] public string? FinanceReviewer { get; set; }
    [Column(IsNullable = true, Position = 26)] public DateTime? FinanceReviewedAt { get; set; }
    [Column(StringLength = 100, IsNullable = true, Position = 27)] public string? BudgetReference { get; set; }
}
