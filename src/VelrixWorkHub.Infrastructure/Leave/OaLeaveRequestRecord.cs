using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Leave;

[Table(Name = "OaLeaveRequest")]
public sealed class OaLeaveRequestRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid UserId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 3)] public OaLeaveType LeaveType { get; set; }
    [Column(IsNullable = false, Position = 4)] public DateTime StartAt { get; set; }
    [Column(IsNullable = false, Position = 5)] public DateTime EndAt { get; set; }
    [Column(StringLength = 1000, IsNullable = false, Position = 6)] public string Reason { get; set; } = string.Empty;
    [Column(StringLength = -1, IsNullable = false, Position = 7)] public string OtherInfo { get; set; } = "{}";
    [Column(MapType = typeof(string), StringLength = 50, Position = 8)] public OaLeaveRequestStatus Status { get; set; }
    [Column(StringLength = 1000, IsNullable = true, Position = 9)] public string? RejectionReason { get; set; }
    [Column(Position = 10, ServerTime = DateTimeKind.Local)] public DateTime CreatedAt { get; set; }
    [Column(Position = 11)] public DateTime? SubmittedAt { get; set; }
}
