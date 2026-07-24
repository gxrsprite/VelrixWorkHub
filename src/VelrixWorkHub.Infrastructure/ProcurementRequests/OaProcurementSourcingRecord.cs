using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.ProcurementRequests;

[Table(Name = "OaProcurementSourcing")]
[Index("OaProcurementSourcing_uk_SourcingNo", nameof(SourcingNo), true)]
[Index("OaProcurementSourcing_uk_ProcurementRequestId", nameof(ProcurementRequestId), true)]
public sealed class OaProcurementSourcingRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 2)] public string SourcingNo { get; set; } = string.Empty;
    [Column(IsNullable = false, Position = 3)] public Guid ProcurementRequestId { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 4)] public string CreatedBy { get; set; } = string.Empty;
    [Column(StringLength = -1, IsNullable = false, Position = 5)] public string OtherInfo { get; set; } = "{}";
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 6)] public OaProcurementSourcingStatus Status { get; set; }
    [Column(IsNullable = true, Position = 7)] public Guid? AwardedQuoteId { get; set; }
    [Column(IsNullable = false, ServerTime = DateTimeKind.Local, Position = 8)] public DateTime CreatedAt { get; set; }
    [Column(IsNullable = true, Position = 9)] public DateTime? AwardedAt { get; set; }
}

[Table(Name = "OaProcurementSourcingQuote")]
[Index("OaProcurementSourcingQuote_ix_SourcingId", nameof(SourcingId), false)]
[Index("OaProcurementSourcingQuote_uk_SourcingSupplierId", nameof(SourcingId) + "," + nameof(SupplierId), true)]
public sealed class OaProcurementSourcingQuoteRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid SourcingId { get; set; }
    [Column(IsNullable = false, Position = 3)] public Guid SupplierId { get; set; }
    [Column(DbType = "numeric(18,2)", IsNullable = false, Position = 4)] public decimal QuoteAmount { get; set; }
    [Column(IsNullable = false, Position = 5)] public int DeliveryDays { get; set; }
    [Column(IsNullable = false, Position = 6)] public DateTime ValidUntil { get; set; }
    [Column(StringLength = 2000, IsNullable = true, Position = 7)] public string? Notes { get; set; }
    [Column(StringLength = -1, IsNullable = false, Position = 8)] public string OtherInfo { get; set; } = "{}";
    [Column(IsNullable = false, ServerTime = DateTimeKind.Local, Position = 9)] public DateTime CreatedAt { get; set; }
}
