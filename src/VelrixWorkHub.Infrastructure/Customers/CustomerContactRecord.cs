using FreeSql.DataAnnotations;
namespace VelrixWorkHub.Infrastructure.Customers;
[Table(Name = "CrmCustomerContact")]
public sealed class CustomerContactRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2)] public Guid CustomerId { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 3)] public string Name { get; set; } = string.Empty;
    [Column(StringLength = 100, Position = 4)] public string? Position { get; set; }
    [Column(StringLength = 50, Position = 5)] public string? Phone { get; set; }
    [Column(StringLength = 200, Position = 6)] public string? Email { get; set; }
    [Column(Position = 7)] public bool IsPrimary { get; set; }
    [Column(Position = 8, ServerTime = DateTimeKind.Local)] public DateTime CreatedTime { get; set; }
    [Column(Position = 9, ServerTime = DateTimeKind.Local)] public DateTime ModifiedTime { get; set; }
}
