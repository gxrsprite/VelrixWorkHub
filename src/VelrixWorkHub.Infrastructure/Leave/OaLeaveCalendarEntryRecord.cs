using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Leave;

[Table(Name = "OaLeaveCalendarEntry")]
[Index("ux_oa_leave_calendar_entry_request", "LeaveRequestId", true)]
[Index("ix_oa_leave_calendar_entry_user_start", "UserId, StartAt")]
public sealed class OaLeaveCalendarEntryRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid LeaveRequestId { get; set; }
    [Column(IsNullable = false, Position = 3)] public Guid UserId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 4)] public OaLeaveType LeaveType { get; set; }
    [Column(IsNullable = false, Position = 5)] public DateTime StartAt { get; set; }
    [Column(IsNullable = false, Position = 6)] public DateTime EndAt { get; set; }
    [Column(StringLength = 1000, IsNullable = false, Position = 7)] public string Reason { get; set; } = string.Empty;
    [Column(IsNullable = false, Position = 8, ServerTime = DateTimeKind.Local)] public DateTime CreatedAt { get; set; }
}
