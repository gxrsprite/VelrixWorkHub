using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomWorkOrderOperation")]
[Index("MomWorkOrderOperation_uk_WorkOrder_Sequence", "WorkOrderId,OperationSequence", true)]
public sealed class MomWorkOrderOperationRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid WorkOrderId { get; set; }
    [Column(Position = 3, IsNullable = false)] public int OperationSequence { get; set; }
    [Column(Position = 4, IsNullable = false, StringLength = 50)] public string OperationCode { get; set; } = string.Empty;
    [Column(Position = 5, IsNullable = false, StringLength = 200)] public string OperationName { get; set; } = string.Empty;
    [Column(Position = 6, IsNullable = false)] public Guid WorkCenterId { get; set; }
    [Column(Position = 7, IsNullable = false, DbType = "numeric(18,6)")] public decimal PlannedQuantity { get; set; }
    [Column(Position = 8, IsNullable = false, DbType = "numeric(18,6)")] public decimal ReportedQuantity { get; set; }
    [Column(Position = 9, IsNullable = false, DbType = "numeric(18,6)")] public decimal GoodQuantity { get; set; }
    [Column(Position = 10, IsNullable = false, DbType = "numeric(18,6)")] public decimal ScrapQuantity { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 11, IsNullable = false)] public MomOperationStatus Status { get; set; }
    [Column(Position = 12, StringLength = 100)] public string? AcceptedBy { get; set; }
    [Column(Position = 13)] public DateTime? AcceptedOn { get; set; }
    [Column(Position = 14)] public DateTime? StartedOn { get; set; }
    [Column(Position = 15)] public DateTime? PausedOn { get; set; }
    [Column(Position = 16)] public DateTime? CompletedOn { get; set; }
    [Column(StringLength = -1, Position = 17, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
    [Column(Position = 18, IsNullable = false, DbType = "numeric(18,6)")] public decimal StandardSetupHours { get; set; }
    [Column(Position = 19, IsNullable = false, DbType = "numeric(18,6)")] public decimal StandardRunHoursPerUnit { get; set; }
}
