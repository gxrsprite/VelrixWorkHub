using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomWorkOrderOperationWorkLog")]
[Index("MomWorkOrderOperationWorkLog_uk_SourceNo", nameof(SourceNo), true)]
public sealed class MomWorkOrderOperationWorkLogRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid OperationId { get; set; }
    [Column(Position = 3, IsNullable = false)] public Guid WorkOrderId { get; set; }
    [Column(Position = 4, IsNullable = false)] public Guid WorkCenterId { get; set; }
    [Column(Position = 5, IsNullable = false)] public Guid OperatorUserId { get; set; }
    [Column(Position = 6, IsNullable = false, StringLength = 200)] public string OperatorName { get; set; } = string.Empty;
    [Column(Position = 7, IsNullable = true)] public Guid? EquipmentId { get; set; }
    [Column(Position = 8, StringLength = 200, IsNullable = true)] public string? EquipmentName { get; set; }
    [Column(Position = 9, IsNullable = false)] public DateTime StartedOn { get; set; }
    [Column(Position = 10, IsNullable = false)] public DateTime EndedOn { get; set; }
    [Column(Position = 11, IsNullable = false, DbType = "numeric(18,6)")] public decimal Hours { get; set; }
    [Column(Position = 12, IsNullable = false, StringLength = 120)] public string SourceNo { get; set; } = string.Empty;
    [Column(Position = 13, StringLength = 2000)] public string? Notes { get; set; }
    [Column(Position = 14, StringLength = -1, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}
