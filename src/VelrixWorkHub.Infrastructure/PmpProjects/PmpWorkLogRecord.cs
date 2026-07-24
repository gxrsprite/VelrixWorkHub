using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.PmpProjects;
[Table(Name = "PmpWorkLog")]
public sealed class PmpWorkLogRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid ProjectId { get; set; }
    [Column(Position = 3)] public Guid? WbsTaskId { get; set; }
    [Column(Position = 4, DbType = "date", IsNullable = false)] public DateTime WorkDate { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 5)] public string MemberName { get; set; } = string.Empty;
    [Column(Position = 6, IsNullable = false, DbType = "numeric(10,2)")] public decimal Hours { get; set; }
    [Column(StringLength = 500, Position = 7)] public string? Note { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = true, Position = 8)] public PmpWorkLogAttendanceStatus? AttendanceStatus { get; set; }
}
