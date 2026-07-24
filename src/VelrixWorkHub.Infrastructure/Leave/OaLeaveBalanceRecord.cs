using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Leave;

[Table(Name = "OaLeaveBalance")]
[Index("OaLeaveBalance_uk_UserYearType", "UserId,Year,LeaveType", true)]
public sealed class OaLeaveBalanceRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid UserId { get; set; }
    [Column(IsNullable = false, Position = 3)] public int Year { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 4)] public OaLeaveType LeaveType { get; set; }
    [Column(DbType = "numeric(18,2)", IsNullable = false, Position = 5)] public decimal EntitledHours { get; set; }
    [Column(DbType = "numeric(18,2)", IsNullable = false, Position = 6)] public decimal ReservedHours { get; set; }
    [Column(DbType = "numeric(18,2)", IsNullable = false, Position = 7)] public decimal UsedHours { get; set; }
    [Column(StringLength = -1, IsNullable = false, Position = 8)] public string OtherInfo { get; set; } = "{}";
    [Column(Position = 9, ServerTime = DateTimeKind.Local)] public DateTime CreatedAt { get; set; }
    [Column(Position = 10, ServerTime = DateTimeKind.Local)] public DateTime UpdatedAt { get; set; }
}
