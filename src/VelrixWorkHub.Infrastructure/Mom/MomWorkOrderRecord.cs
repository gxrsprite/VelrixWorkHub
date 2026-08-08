using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomWorkOrder")]
public sealed class MomWorkOrderRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 80, IsNullable = false, Position = 2)] public string WorkOrderNo { get; set; } = string.Empty;
    [Column(Position = 3, IsNullable = false)] public Guid ProductId { get; set; }
    [Column(Position = 4)] public Guid? WorkCenterId { get; set; }
    [Column(Position = 5)] public Guid? SalesOrderId { get; set; }
    [Column(Position = 6)] public Guid? PmsProjectId { get; set; }
    [Column(Position = 7, DbType = "date", IsNullable = false)] public DateTime PlannedStart { get; set; }
    [Column(Position = 8, DbType = "date", IsNullable = false)] public DateTime PlannedEnd { get; set; }
    [Column(Position = 9, IsNullable = false, DbType = "numeric(18,4)")] public decimal PlannedQuantity { get; set; }
    [Column(Position = 10, IsNullable = false, DbType = "numeric(18,4)")] public decimal CompletedQuantity { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 11, IsNullable = false)] public MomWorkOrderStatus Status { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 12, IsNullable = false)] public MomWorkOrderSourceKind SourceKind { get; set; }
    [Column(StringLength = 120, Position = 13)] public string? SourceDocumentNo { get; set; }
    [Column(StringLength = -1, Position = 14, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}
