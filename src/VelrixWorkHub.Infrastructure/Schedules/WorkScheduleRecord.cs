using FreeSql.DataAnnotations;
namespace VelrixWorkHub.Infrastructure.Schedules;
[Table(Name = "OaWorkSchedule")]
public sealed class WorkScheduleRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 200, IsNullable = false, Position = 2)] public string Title { get; set; } = string.Empty;
    [Column(StringLength = 4000, Position = 3)] public string? Description { get; set; }
    [Column(StringLength = 300, Position = 4)] public string? Location { get; set; }
    [Column(Position = 5)] public DateTime StartTime { get; set; }
    [Column(Position = 6)] public DateTime EndTime { get; set; }
    [Column(Position = 7, ServerTime = DateTimeKind.Local)] public DateTime CreatedTime { get; set; }
    [Column(Position = 8, ServerTime = DateTimeKind.Local)] public DateTime ModifiedTime { get; set; }
}
