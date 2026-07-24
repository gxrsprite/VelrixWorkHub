using FreeSql.DataAnnotations;

namespace VelrixWorkHub.Infrastructure.PmpProjects;

[Table(Name = "PmpProjectMember")]
public sealed class PmpProjectMemberRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid ProjectId { get; set; }
    [Column(IsNullable = true, Position = 3)] public Guid? UserId { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 4)] public string MemberName { get; set; } = string.Empty;
    [Column(StringLength = 100, IsNullable = false, Position = 5)] public string RoleName { get; set; } = string.Empty;
    [Column(Position = 6)] public bool IsPrimary { get; set; }
    [Column(StringLength = 100, IsNullable = true, Position = 7)] public string? DepartmentName { get; set; }
    [Column(Position = 8, ServerTime = DateTimeKind.Local)] public DateTime CreatedTime { get; set; }
    [Column(Position = 9, ServerTime = DateTimeKind.Local)] public DateTime ModifiedTime { get; set; }
}
