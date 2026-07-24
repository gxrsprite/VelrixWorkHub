using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Tasks;

[Table(Name = "OaWorkTask")]
public sealed class WorkTaskRecord
{
    [Column(IsPrimary = true, Position = 1)]
    public Guid Id { get; set; }

    [Column(StringLength = 200, IsNullable = false, Position = 2)]
    public string Title { get; set; } = string.Empty;

    [Column(StringLength = 4000, Position = 3)]
    public string? Description { get; set; }

    [Column(MapType = typeof(string), StringLength = 50, Position = 4)]
    public WorkTaskStatus Status { get; set; }

    [Column(Position = 5)]
    public DateTime? DueDate { get; set; }

    [Column(Position = 6, ServerTime = DateTimeKind.Local)]
    public DateTime CreatedTime { get; set; }

    [Column(Position = 7, ServerTime = DateTimeKind.Local)]
    public DateTime ModifiedTime { get; set; }
}
