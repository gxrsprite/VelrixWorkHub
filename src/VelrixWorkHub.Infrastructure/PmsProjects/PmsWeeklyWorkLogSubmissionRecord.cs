using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmsProjects;

[Table(Name = "PmsWeeklyWorkLogSubmission")]
public sealed class PmsWeeklyWorkLogSubmissionRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid ProjectId { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 3)] public string MemberName { get; set; } = string.Empty;
    [Column(IsNullable = false, Position = 4)] public DateTime WeekStart { get; set; }
    [Column(StringLength = 16000, IsNullable = false, Position = 5)] public string SnapshotJson { get; set; } = "[]";
    [Column(IsNullable = false, DbType = "numeric(10,2)", Position = 6)] public decimal TotalHours { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 7)] public PmsWeeklyWorkLogSubmissionStatus Status { get; set; }
    [Column(StringLength = 100, IsNullable = true, Position = 8)] public string? SubmittedBy { get; set; }
    [Column(IsNullable = true, Position = 9)] public DateTime? SubmittedAt { get; set; }
    [Column(StringLength = 1000, IsNullable = true, Position = 10)] public string? RejectionReason { get; set; }
    [Column(StringLength = 200, IsNullable = true, Position = 11)] public string? ActiveWeekKey { get; set; }
    [Column(ServerTime = DateTimeKind.Local, Position = 12)] public DateTime CreatedTime { get; set; }
    [Column(ServerTime = DateTimeKind.Local, Position = 13)] public DateTime ModifiedTime { get; set; }
}
