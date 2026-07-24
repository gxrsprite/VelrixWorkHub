using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Lms;

[Table(Name = "LmsCustomerMachine")]
[Index("LmsCustomerMachine_uk_MachineCode", "MachineCode", true)]
public sealed class LmsCustomerMachineRecord
{
    [Column(IsPrimary = true)] public Guid Id { get; set; }
    [Column(IsNullable = false)] public Guid CustomerId { get; set; }
    [Column(StringLength = 160, IsNullable = false)] public string MachineCode { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false)] public string ProductName { get; set; } = string.Empty;
    [Column(StringLength = 200)] public string? Model { get; set; }
    [Column(StringLength = 200)] public string? Environment { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false)] public LmsCustomerMachineStatus Status { get; set; }
    [Column(StringLength = -1, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
    [Column(IsNullable = false)] public DateTime CreatedAt { get; set; }
}
