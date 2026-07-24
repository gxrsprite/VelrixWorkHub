using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Customers;
[Table(Name = "CrmSalesOpportunity")]
public sealed class SalesOpportunityRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2)] public Guid CustomerId { get; set; }
    [Column(StringLength = 200, IsNullable = false, Position = 3)] public string Title { get; set; } = string.Empty;
    [Column(MapType = typeof(string), StringLength = 50, Position = 4)] public OpportunityStage Stage { get; set; }
    [Column(Position = 5, DbType = "numeric(18,2)")] public decimal? ExpectedAmount { get; set; }
    [Column(Position = 6)] public DateTime? ExpectedCloseDate { get; set; }
    [Column(StringLength = 1000, Position = 7)] public string? LostReason { get; set; }
    [Column(Position = 8, ServerTime = DateTimeKind.Local)] public DateTime CreatedTime { get; set; }
    [Column(Position = 9, ServerTime = DateTimeKind.Local)] public DateTime ModifiedTime { get; set; }
}
