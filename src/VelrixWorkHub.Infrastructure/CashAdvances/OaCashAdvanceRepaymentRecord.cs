using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.CashAdvances;

[Table(Name = "OaCashAdvanceRepayment")]
[Index("OaCashAdvanceRepayment_uk_DocumentNo", nameof(DocumentNo), true)]
[Index("OaCashAdvanceRepayment_ix_CashAdvanceId", nameof(CashAdvanceId), false)]
[Index("OaCashAdvanceRepayment_ix_ApplicantUserId", nameof(ApplicantUserId), false)]
public sealed class OaCashAdvanceRepaymentRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid CashAdvanceId { get; set; }
    [Column(IsNullable = false, Position = 3)] public Guid ApplicantUserId { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 4)] public string ApplicantName { get; set; } = string.Empty;
    [Column(StringLength = 100, IsNullable = false, Position = 5)] public string DepartmentName { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 6)] public string LegalEntity { get; set; } = string.Empty;
    [Column(StringLength = 100, IsNullable = false, Position = 7)] public string DocumentNo { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 8)] public string Title { get; set; } = string.Empty;
    [Column(DbType = "numeric(18,2)", IsNullable = false, Position = 9)] public decimal Amount { get; set; }
    [Column(IsNullable = false, Position = 10)] public DateTime RepaymentDate { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 11)] public OaCashAdvanceRepaymentMethod RepaymentMethod { get; set; }
    [Column(StringLength = 200, IsNullable = false, Position = 12)] public string ReceiptReference { get; set; } = string.Empty;
    [Column(StringLength = 2000, IsNullable = false, Position = 13)] public string Notes { get; set; } = string.Empty;
    [Column(StringLength = -1, IsNullable = false, Position = 14)] public string OtherInfo { get; set; } = "{}";
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 15)] public OaCashAdvanceRepaymentStatus Status { get; set; }
    [Column(StringLength = 1000, IsNullable = true, Position = 16)] public string? RejectionReason { get; set; }
    [Column(IsNullable = false, ServerTime = DateTimeKind.Local, Position = 17)] public DateTime CreatedAt { get; set; }
    [Column(IsNullable = true, Position = 18)] public DateTime? SubmittedAt { get; set; }
}
