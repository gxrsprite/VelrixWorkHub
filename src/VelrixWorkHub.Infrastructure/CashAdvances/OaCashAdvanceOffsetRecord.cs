using FreeSql.DataAnnotations;

namespace VelrixWorkHub.Infrastructure.CashAdvances;

[Table(Name = "OaCashAdvanceOffset")]
[Index("OaCashAdvanceOffset_uk_ReimbursementId", nameof(ReimbursementId), true)]
public sealed class OaCashAdvanceOffsetRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid CashAdvanceId { get; set; }
    [Column(IsNullable = false, Position = 3)] public Guid ReimbursementId { get; set; }
    [Column(DbType = "numeric(18,2)", IsNullable = false, Position = 4)] public decimal Amount { get; set; }
    [Column(IsNullable = false, Position = 5)] public DateTime OffsetDate { get; set; }
    [Column(StringLength = 1000, IsNullable = false, Position = 6)] public string Notes { get; set; } = string.Empty;
    [Column(StringLength = -1, IsNullable = false, Position = 7)] public string OtherInfo { get; set; } = "{}";
}
