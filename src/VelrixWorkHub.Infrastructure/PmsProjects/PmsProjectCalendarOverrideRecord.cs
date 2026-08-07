using FreeSql.DataAnnotations;

namespace VelrixWorkHub.Infrastructure.PmsProjects;

[Table(Name = "PmsProjectCalendarOverride")]
public sealed class PmsProjectCalendarOverrideRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid ProjectId { get; set; }
    [Column(DbType = "date", IsNullable = false, Position = 3)] public DateTime Date { get; set; }
    [Column(IsNullable = false, Position = 4)] public bool IsWorkingDay { get; set; }
    [Column(StringLength = 500, IsNullable = true, Position = 5)] public string? Note { get; set; }
    [Column(ServerTime = DateTimeKind.Local, Position = 6)] public DateTime CreatedTime { get; set; }
    [Column(ServerTime = DateTimeKind.Local, Position = 7)] public DateTime ModifiedTime { get; set; }
}
