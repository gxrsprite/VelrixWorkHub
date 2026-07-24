using FreeSql.DataAnnotations;

namespace VelrixWorkHub.Infrastructure.ExpenseReimbursements;

[Table(Name = "OaExpenseLine")]
public sealed class OaExpenseLineRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid ReimbursementId { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 3)] public string ExpenseType { get; set; } = string.Empty;
    [Column(StringLength = 1000, IsNullable = false, Position = 4)] public string Description { get; set; } = string.Empty;
    [Column(StringLength = 100, IsNullable = true, Position = 5)] public string? InvoiceNo { get; set; }
    [Column(StringLength = 100, IsNullable = true, Position = 6)] public string? PaymentFlowNo { get; set; }
    [Column(IsNullable = false, Position = 7)] public DateTime BusinessDate { get; set; }
    [Column(DbType = "numeric(18,2)", IsNullable = false, Position = 8)] public decimal Amount { get; set; }
    [Column(DbType = "numeric(18,2)", IsNullable = false, Position = 9)] public decimal ActualAmount { get; set; }
    [Column(IsNullable = true, Position = 10)] public Guid? ProjectId { get; set; }
    [Column(StringLength = -1, IsNullable = false, Position = 11)] public string OtherInfo { get; set; } = "{}";
}
