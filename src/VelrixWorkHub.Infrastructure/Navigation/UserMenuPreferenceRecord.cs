using FreeSql.DataAnnotations;

namespace VelrixWorkHub.Infrastructure.Navigation;

[Table(Name = "SysUserMenuPreference")]
public sealed class UserMenuPreferenceRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2)] public Guid UserId { get; set; }
    [Column(Position = 3)] public Guid MenuId { get; set; }
    [Column(Position = 4)] public bool IsVisible { get; set; }
    [Column(Position = 5)] public bool IsFavorite { get; set; }
    [Column(Position = 6)] public int Sort { get; set; }
    [Column(Position = 7, ServerTime = DateTimeKind.Local)] public DateTime ModifiedTime { get; set; }
}
