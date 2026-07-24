using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.ProcurementRequests;

[Table(Name = "OaProcurementBudget")]
[Index("OaProcurementBudget_uk_BudgetNo", nameof(BudgetNo), true)]
public sealed class OaProcurementBudgetRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 2)] public string BudgetNo { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 3)] public string LegalEntity { get; set; } = string.Empty;
    [Column(StringLength = 100, IsNullable = false, Position = 4)] public string DepartmentName { get; set; } = string.Empty;
    [Column(DbType = "numeric(18,2)", IsNullable = false, Position = 5)] public decimal TotalAmount { get; set; }
    [Column(DbType = "numeric(18,2)", IsNullable = false, Position = 6)] public decimal ReservedAmount { get; set; }
    [Column(DbType = "numeric(18,2)", IsNullable = false, Position = 7)] public decimal ConsumedAmount { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 8)] public OaProcurementBudgetStatus Status { get; set; }
    [Column(StringLength = -1, IsNullable = false, Position = 9)] public string OtherInfo { get; set; } = "{}";
    [Column(IsNullable = false, ServerTime = DateTimeKind.Local, Position = 10)] public DateTime CreatedAt { get; set; }
}

[Table(Name = "OaProcurementBudgetReservation")]
[Index("OaProcurementBudgetReservation_uk_ProcurementRequestId", nameof(ProcurementRequestId), true)]
[Index("OaProcurementBudgetReservation_ix_BudgetId", nameof(BudgetId), false)]
public sealed class OaProcurementBudgetReservationRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid BudgetId { get; set; }
    [Column(IsNullable = false, Position = 3)] public Guid ProcurementRequestId { get; set; }
    [Column(DbType = "numeric(18,2)", IsNullable = false, Position = 4)] public decimal Amount { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 5)] public OaProcurementBudgetReservationStatus Status { get; set; }
    [Column(IsNullable = false, ServerTime = DateTimeKind.Local, Position = 6)] public DateTime CreatedAt { get; set; }
    [Column(IsNullable = true, Position = 7)] public DateTime? CompletedAt { get; set; }
}
