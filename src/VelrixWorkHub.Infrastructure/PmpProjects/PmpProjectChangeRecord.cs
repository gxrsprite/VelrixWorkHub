using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.PmpProjects;
[Table(Name = "PmpProjectChange")]
public sealed class PmpProjectChangeRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid ProjectId { get; set; }
    [Column(StringLength = 200, IsNullable = false, Position = 3)] public string Title { get; set; } = string.Empty;
    [Column(StringLength = 1000, IsNullable = false, Position = 4)] public string Reason { get; set; } = string.Empty;
    [Column(StringLength = 1000, Position = 5)] public string? Impact { get; set; }
    [Column(StringLength = 100, Position = 6)] public string? RequesterName { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 7, IsNullable = false)] public PmpProjectChangeStatus Status { get; set; }
    [Column(Position = 8, IsNullable = false, ServerTime = DateTimeKind.Local)] public DateTime CreatedTime { get; set; }
}
