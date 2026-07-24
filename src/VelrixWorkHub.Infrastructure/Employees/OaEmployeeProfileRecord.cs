using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Employees;

[Table(Name = "OaEmployeeProfile")]
[Index("OaEmployeeProfile_uk_UserId", nameof(UserId), true)]
public sealed class OaEmployeeProfileRecord
{
    [Column(IsPrimary = true, Position = 1)]
    public Guid UserId { get; set; }

    [Column(StringLength = 80, Position = 2)]
    public string? EmployeeNo { get; set; }

    [Column(StringLength = 50, Position = 3)]
    public string? Phone { get; set; }

    [Column(StringLength = 200, Position = 4)]
    public string? Email { get; set; }

    [Column(StringLength = 200, Position = 5)]
    public string? WeComUserId { get; set; }

    [Column(StringLength = 200, Position = 6)]
    public string? DingTalkUserId { get; set; }

    [Column(StringLength = 100, Position = 7)]
    public string? PositionTitle { get; set; }

    [Column(Position = 8)]
    public DateTime? HireDate { get; set; }

    [Column(MapType = typeof(string), StringLength = 50, Position = 9)]
    public OaEmploymentStatus Status { get; set; }

    [Column(StringLength = -1, IsNullable = false, Position = 10)]
    public string OtherInfo { get; set; } = "{}";
}
