using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomAcceptance")]
[Index("MomAcceptance_uk_AcceptanceNo", "AcceptanceNo", true)]
[Index("MomAcceptance_ix_SalesOrderId", "SalesOrderId", false)]
public sealed class MomAcceptanceRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, StringLength = 80, IsNullable = false)] public string AcceptanceNo { get; set; } = string.Empty;
    [Column(MapType = typeof(string), StringLength = 50, Position = 3, IsNullable = false)] public MomAcceptanceType AcceptanceType { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 4, IsNullable = false)] public MomAcceptanceStatus Status { get; set; }
    [Column(Position = 5, IsNullable = false)] public Guid SalesOrderId { get; set; }
    [Column(Position = 6, IsNullable = true)] public Guid? ShipmentId { get; set; }
    [Column(Position = 7, IsNullable = true)] public Guid? PmsProjectId { get; set; }
    [Column(Position = 8, IsNullable = false)] public Guid CustomerId { get; set; }
    [Column(Position = 9, IsNullable = false)] public Guid ProductId { get; set; }
    [Column(Position = 10, StringLength = 100)] public string? SerialNo { get; set; }
    [Column(Position = 11, IsNullable = false)] public DateTime PlannedDate { get; set; }
    [Column(Position = 12, StringLength = 200)] public string? LocationOrMode { get; set; }
    [Column(Position = 13, StringLength = 500)] public string? Participants { get; set; }
    [Column(Position = 14, StringLength = 100, IsNullable = false)] public string CreatedBy { get; set; } = string.Empty;
    [Column(Position = 15, IsNullable = false)] public DateTime CreatedOn { get; set; }
    [Column(Position = 16, StringLength = 100)] public string? SubmittedBy { get; set; }
    [Column(Position = 17)] public DateTime? SubmittedOn { get; set; }
    [Column(Position = 18, StringLength = 100)] public string? CompletedBy { get; set; }
    [Column(Position = 19)] public DateTime? CompletedOn { get; set; }
    [Column(Position = 20, StringLength = 1000)] public string? Conclusion { get; set; }
    [Column(Position = 21, StringLength = 1000)] public string? FailureReason { get; set; }
    [Column(Position = 22, StringLength = 100)] public string? CancelledBy { get; set; }
    [Column(Position = 23)] public DateTime? CancelledOn { get; set; }
    [Column(Position = 24, StringLength = 1000)] public string? CancellationReason { get; set; }
    [Column(Position = 25, StringLength = 1000)] public string? Notes { get; set; }
    [Column(StringLength = -1, Position = 26, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}
