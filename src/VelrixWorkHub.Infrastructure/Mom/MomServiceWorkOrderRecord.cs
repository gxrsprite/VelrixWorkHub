using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomServiceWorkOrder")]
[Index("MomServiceWorkOrder_uk_WorkOrderNo", "WorkOrderNo", true)]
[Index("MomServiceWorkOrder_uk_OpenKey", "OpenKey", true)]
[Index("MomServiceWorkOrder_ix_EquipmentId_Status", "EquipmentId,Status", false)]
public sealed class MomServiceWorkOrderRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, StringLength = 80, IsNullable = false)] public string WorkOrderNo { get; set; } = string.Empty;
    [Column(MapType = typeof(string), StringLength = 50, Position = 3, IsNullable = false)] public MomServiceWorkOrderType Type { get; set; }
    [Column(Position = 4, IsNullable = false)] public Guid EquipmentId { get; set; }
    [Column(Position = 5, IsNullable = true)] public DateTime? ScheduledOn { get; set; }
    [Column(Position = 6, StringLength = 100, IsNullable = true)] public string? AssignedTo { get; set; }
    [Column(Position = 7, StringLength = 300, IsNullable = false)] public string PlannedLocation { get; set; } = string.Empty;
    [Column(Position = 8, StringLength = 2000, IsNullable = false)] public string Description { get; set; } = string.Empty;
    [Column(MapType = typeof(string), StringLength = 50, Position = 9, IsNullable = false)] public MomServiceWorkOrderStatus Status { get; set; }
    [Column(Position = 10, StringLength = 100, IsNullable = false)] public string CreatedBy { get; set; } = string.Empty;
    [Column(Position = 11, IsNullable = false)] public DateTime CreatedOn { get; set; }
    [Column(Position = 12, StringLength = 100, IsNullable = true)] public string? StartedBy { get; set; }
    [Column(Position = 13, IsNullable = true)] public DateTime? StartedOn { get; set; }
    [Column(Position = 14, StringLength = 100, IsNullable = true)] public string? CompletedBy { get; set; }
    [Column(Position = 15, IsNullable = true)] public DateTime? CompletedOn { get; set; }
    [Column(Position = 16, StringLength = 2000, IsNullable = true)] public string? CompletionNotes { get; set; }
    [Column(Position = 17, StringLength = 100, IsNullable = true)] public string? CancelledBy { get; set; }
    [Column(Position = 18, IsNullable = true)] public DateTime? CancelledOn { get; set; }
    [Column(Position = 19, StringLength = 1000, IsNullable = true)] public string? CancellationReason { get; set; }
    [Column(Position = 20, StringLength = 80, IsNullable = false)] public string OpenKey { get; set; } = string.Empty;
    [Column(StringLength = -1, Position = 21, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}

[Table(Name = "MomServiceWorkOrderHistory")]
[Index("MomServiceWorkOrderHistory_ix_WorkOrderId_OccurredOn", "WorkOrderId,OccurredOn", false)]
public sealed class MomServiceWorkOrderHistoryRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid WorkOrderId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 3, IsNullable = false)] public MomServiceWorkOrderHistoryAction Action { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 4, IsNullable = false)] public MomServiceWorkOrderStatus ToStatus { get; set; }
    [Column(Position = 5, StringLength = 100, IsNullable = false)] public string Actor { get; set; } = string.Empty;
    [Column(Position = 6, IsNullable = false)] public DateTime OccurredOn { get; set; }
    [Column(Position = 7, StringLength = 2000, IsNullable = true)] public string? Note { get; set; }
    [Column(StringLength = -1, Position = 8, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}
