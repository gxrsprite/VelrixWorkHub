using FreeSql.DataAnnotations;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomWorkOrderOperationReport")]
[Index("MomWorkOrderOperationReport_uk_SourceNo", "SourceNo", true)]
public sealed class MomWorkOrderOperationReportRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid OperationId { get; set; }
    [Column(Position = 3, IsNullable = false)] public Guid WorkOrderId { get; set; }
    [Column(Position = 4, IsNullable = false)] public Guid WorkCenterId { get; set; }
    [Column(Position = 5, IsNullable = false, DbType = "numeric(18,6)")] public decimal Quantity { get; set; }
    [Column(Position = 6, IsNullable = false, DbType = "numeric(18,6)")] public decimal GoodQuantity { get; set; }
    [Column(Position = 7, IsNullable = false, DbType = "numeric(18,6)")] public decimal ScrapQuantity { get; set; }
    [Column(Position = 8, IsNullable = false, StringLength = 80)] public string SourceNo { get; set; } = string.Empty;
    [Column(Position = 9, IsNullable = false)] public DateTime OccurredOn { get; set; }
    [Column(Position = 10, IsNullable = false, StringLength = 100)] public string Actor { get; set; } = string.Empty;
    [Column(Position = 11, StringLength = 500)] public string? Notes { get; set; }
    [Column(StringLength = -1, Position = 12, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}
