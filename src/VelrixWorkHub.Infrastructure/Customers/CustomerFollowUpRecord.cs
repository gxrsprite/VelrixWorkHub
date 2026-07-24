using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Customers;
[Table(Name = "CrmCustomerFollowUp")]
public sealed class CustomerFollowUpRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2)] public Guid CustomerId { get; set; }
    [Column(Position = 3)] public Guid? ContactId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 4)] public FollowUpType Type { get; set; }
    [Column(StringLength = 5000, IsNullable = false, Position = 5)] public string Content { get; set; } = string.Empty;
    [Column(Position = 6)] public DateTime? NextFollowUpDate { get; set; }
    [Column(Position = 7, ServerTime = DateTimeKind.Local)] public DateTime CreatedTime { get; set; }
}
