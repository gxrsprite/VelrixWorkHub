using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.ExpenseReimbursements;

[Table(Name = "OaExpenseReimbursement")]
[Index("OaExpenseReimbursement_uk_DocumentNo", nameof(DocumentNo), true)]
public sealed class OaExpenseReimbursementRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 2)] public string DocumentNo { get; set; } = string.Empty;
    [Column(IsNullable = false, Position = 3)] public Guid ApplicantUserId { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 4)] public string ApplicantName { get; set; } = string.Empty;
    [Column(StringLength = 100, IsNullable = false, Position = 5)] public string DepartmentName { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 6)] public string LegalEntity { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 7)] public string Title { get; set; } = string.Empty;
    [Column(IsNullable = false, Position = 8)] public DateTime ReimbursementDate { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 9)] public OaExpenseReimbursementType ReimbursementType { get; set; }
    [Column(IsNullable = true, Position = 10)] public Guid? ProjectId { get; set; }
    [Column(IsNullable = false, Position = 11)] public bool IsEntrusted { get; set; }
    [Column(IsNullable = false, Position = 12)] public bool IsTeamBuilding { get; set; }
    [Column(IsNullable = false, Position = 13)] public bool IsEntertainment { get; set; }
    [Column(DbType = "numeric(18,2)", IsNullable = false, Position = 14)] public decimal ActualAmount { get; set; }
    [Column(StringLength = 2000, IsNullable = false, Position = 15)] public string Reason { get; set; } = string.Empty;
    [Column(StringLength = -1, IsNullable = false, Position = 16)] public string OtherInfo { get; set; } = "{}";
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 17)] public OaExpenseReimbursementStatus Status { get; set; }
    [Column(StringLength = 1000, IsNullable = true, Position = 18)] public string? RejectionReason { get; set; }
    [Column(IsNullable = false, ServerTime = DateTimeKind.Local, Position = 19)] public DateTime CreatedAt { get; set; }
    [Column(IsNullable = true, Position = 20)] public DateTime? SubmittedAt { get; set; }
}
