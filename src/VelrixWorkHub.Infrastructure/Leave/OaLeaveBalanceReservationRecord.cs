using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Leave;

[Table(Name = "OaLeaveBalanceReservation")]
[Index("OaLeaveBalanceReservation_uk_RequestId", nameof(RequestId), true)]
public sealed class OaLeaveBalanceReservationRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid BalanceId { get; set; }
    [Column(IsNullable = false, Position = 3)] public Guid RequestId { get; set; }
    [Column(DbType = "numeric(18,2)", IsNullable = false, Position = 4)] public decimal Hours { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 5)] public OaLeaveBalanceReservationStatus Status { get; set; }
    [Column(Position = 6, ServerTime = DateTimeKind.Local)] public DateTime CreatedAt { get; set; }
    [Column(Position = 7)] public DateTime? ReleasedAt { get; set; }
}
