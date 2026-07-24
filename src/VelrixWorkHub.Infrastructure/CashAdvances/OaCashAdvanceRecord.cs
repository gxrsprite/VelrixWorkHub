using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.CashAdvances;

[Table(Name = "OaCashAdvance")]
[Index("OaCashAdvance_uk_DocumentNo", nameof(DocumentNo), true)]
public sealed class OaCashAdvanceRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 2)] public string DocumentNo { get; set; } = string.Empty;
    [Column(IsNullable = false, Position = 3)] public Guid ApplicantUserId { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 4)] public string ApplicantName { get; set; } = string.Empty;
    [Column(StringLength = 100, IsNullable = false, Position = 5)] public string DepartmentName { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 6)] public string LegalEntity { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 7)] public string Title { get; set; } = string.Empty;
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 8)] public OaCashAdvanceType AdvanceType { get; set; }
    [Column(IsNullable = false, Position = 9)] public DateTime RequestDate { get; set; }
    [Column(IsNullable = false, Position = 10)] public DateTime ExpectedSettlementDate { get; set; }
    [Column(IsNullable = true, Position = 11)] public Guid? ProjectId { get; set; }
    [Column(DbType = "numeric(18,2)", IsNullable = false, Position = 12)] public decimal Amount { get; set; }
    [Column(DbType = "numeric(18,2)", IsNullable = false, Position = 13)] public decimal SettledAmount { get; set; }
    [Column(StringLength = 2000, IsNullable = false, Position = 14)] public string Purpose { get; set; } = string.Empty;
    [Column(StringLength = -1, IsNullable = false, Position = 15)] public string OtherInfo { get; set; } = "{}";
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 16)] public OaCashAdvanceStatus Status { get; set; }
    [Column(StringLength = 1000, IsNullable = true, Position = 17)] public string? RejectionReason { get; set; }
    [Column(IsNullable = false, ServerTime = DateTimeKind.Local, Position = 18)] public DateTime CreatedAt { get; set; }
    [Column(IsNullable = true, Position = 19)] public DateTime? SubmittedAt { get; set; }
}
