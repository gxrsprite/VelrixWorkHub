using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Overtime;

[Table(Name = "OaOvertimeConversion")]
[Index("OaOvertimeConversion_uk_Request", "OvertimeRequestId", true)]
public sealed class OaOvertimeConversionRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid OvertimeRequestId { get; set; }
    [Column(IsNullable = false, Position = 3)] public Guid UserId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 4)] public OaOvertimeConversionType Type { get; set; }
    [Column(DbType = "numeric(18,2)", IsNullable = false, Position = 5)] public decimal Hours { get; set; }
    [Column(IsNullable = false, ServerTime = DateTimeKind.Local, Position = 6)] public DateTime CreatedAt { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 7)] public OaOvertimeFinanceProcessingStatus FinanceProcessingStatus { get; set; }
    [Column(StringLength = 100, Position = 8)] public string? FinanceProcessedBy { get; set; }
    [Column(Position = 9)] public DateTime? FinanceProcessedAt { get; set; }
    [Column(StringLength = 1000, Position = 10)] public string? FinanceProcessingNote { get; set; }
}
