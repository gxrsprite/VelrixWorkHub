using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.ProcurementRequests;

[Table(Name = "OaProcurementRequest")]
[Index("OaProcurementRequest_uk_DocumentNo", nameof(DocumentNo), true)]
public sealed class OaProcurementRequestRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 2)] public string DocumentNo { get; set; } = string.Empty;
    [Column(IsNullable = false, Position = 3)] public Guid ApplicantUserId { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 4)] public string ApplicantName { get; set; } = string.Empty;
    [Column(StringLength = 100, IsNullable = false, Position = 5)] public string DepartmentName { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 6)] public string LegalEntity { get; set; } = string.Empty;
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 7)] public OaProcurementRequestType RequestType { get; set; }
    [Column(IsNullable = false, Position = 8)] public DateTime RequestDate { get; set; }
    [Column(IsNullable = false, Position = 9)] public DateTime RequiredDate { get; set; }
    [Column(IsNullable = true, Position = 10)] public Guid? ProjectId { get; set; }
    [Column(StringLength = 200, IsNullable = true, Position = 11)] public string? BudgetReference { get; set; }
    [Column(DbType = "numeric(18,2)", IsNullable = false, Position = 12)] public decimal EstimatedAmount { get; set; }
    [Column(StringLength = 2000, IsNullable = false, Position = 13)] public string Purpose { get; set; } = string.Empty;
    [Column(StringLength = -1, IsNullable = false, Position = 14)] public string OtherInfo { get; set; } = "{}";
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 15)] public OaProcurementRequestStatus Status { get; set; }
    [Column(StringLength = 1000, IsNullable = true, Position = 16)] public string? RejectionReason { get; set; }
    [Column(IsNullable = false, ServerTime = DateTimeKind.Local, Position = 17)] public DateTime CreatedAt { get; set; }
    [Column(IsNullable = true, Position = 18)] public DateTime? SubmittedAt { get; set; }
}

[Table(Name = "OaProcurementRequestLine")]
[Index("OaProcurementRequestLine_ix_RequestId", nameof(RequestId), false)]
public sealed class OaProcurementRequestLineRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid RequestId { get; set; }
    [Column(IsNullable = true, Position = 3)] public Guid? ProductId { get; set; }
    [Column(StringLength = 200, IsNullable = false, Position = 4)] public string ItemName { get; set; } = string.Empty;
    [Column(StringLength = 100, IsNullable = false, Position = 5)] public string MaterialCategory { get; set; } = string.Empty;
    [Column(StringLength = 1000, IsNullable = false, Position = 6)] public string Specification { get; set; } = string.Empty;
    [Column(DbType = "numeric(18,4)", IsNullable = false, Position = 7)] public decimal Quantity { get; set; }
    [Column(StringLength = 50, IsNullable = false, Position = 8)] public string Unit { get; set; } = string.Empty;
    [Column(DbType = "numeric(18,2)", IsNullable = false, Position = 9)] public decimal EstimatedUnitPrice { get; set; }
    [Column(StringLength = -1, IsNullable = false, Position = 10)] public string OtherInfo { get; set; } = "{}";
}
