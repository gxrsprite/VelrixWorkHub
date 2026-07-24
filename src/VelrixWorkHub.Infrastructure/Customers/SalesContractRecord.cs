using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Customers;
[Table(Name = "CrmSalesContract")]
public sealed class SalesContractRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2)] public Guid CustomerId { get; set; }
    [Column(Position = 3)] public Guid? OpportunityId { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 4)] public string ContractNo { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 5)] public string Title { get; set; } = string.Empty;
    [Column(Position = 6, DbType = "numeric(18,2)")] public decimal Amount { get; set; }
    [Column(Position = 7)] public DateTime StartDate { get; set; }
    [Column(Position = 8)] public DateTime EndDate { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 9)] public ContractStatus Status { get; set; }
    [Column(Position = 10, ServerTime = DateTimeKind.Local)] public DateTime CreatedTime { get; set; }
    [Column(Position = 11, ServerTime = DateTimeKind.Local)] public DateTime ModifiedTime { get; set; }
}
