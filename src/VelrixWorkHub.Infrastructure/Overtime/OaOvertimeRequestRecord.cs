using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Overtime;

[Table(Name = "OaOvertimeRequest")]
public sealed class OaOvertimeRequestRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid UserId { get; set; }
    [Column(IsNullable = false, Position = 3)] public DateTime StartAt { get; set; }
    [Column(IsNullable = false, Position = 4)] public DateTime EndAt { get; set; }
    [Column(StringLength = 1000, IsNullable = false, Position = 5)] public string Reason { get; set; } = string.Empty;
    [Column(StringLength = -1, IsNullable = false, Position = 6)] public string OtherInfo { get; set; } = "{}";
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 7)] public OaOvertimeRequestStatus Status { get; set; }
    [Column(StringLength = 1000, IsNullable = true, Position = 8)] public string? RejectionReason { get; set; }
    [Column(IsNullable = false, ServerTime = DateTimeKind.Local, Position = 9)] public DateTime CreatedAt { get; set; }
    [Column(IsNullable = true, Position = 10)] public DateTime? SubmittedAt { get; set; }
}
