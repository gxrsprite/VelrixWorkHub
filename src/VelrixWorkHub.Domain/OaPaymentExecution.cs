namespace VelrixWorkHub.Domain;

public enum OaPaymentExecutionChannel
{
    BankTransfer,
    Cash,
    Other
}

/// <summary>付款申请的实际付款记录；不代表银行接口已成功回执。</summary>
public sealed class OaPaymentExecution
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid PaymentRequestId { get; private set; }
    public string ExecutionNo { get; private set; } = string.Empty;
    public DateOnly PaidOn { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "CNY";
    public OaPaymentExecutionChannel Channel { get; private set; }
    public string ExternalReference { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public Guid? ErpSettlementId { get; private set; }
    public string Operator { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    public OaPaymentExecution(Guid paymentRequestId, string executionNo, DateOnly paidOn, decimal amount, string currency,
        OaPaymentExecutionChannel channel, string externalReference, string? notes, Guid? erpSettlementId,
        string @operator, DateTime createdAt)
    {
        if (paymentRequestId == Guid.Empty) throw new ArgumentException("付款申请不能为空。", nameof(paymentRequestId));
        PaymentRequestId = paymentRequestId;
        ExecutionNo = Required(executionNo, "实际付款流水号");
        if (paidOn == default) throw new ArgumentException("实际付款日期不能为空。", nameof(paidOn));
        PaidOn = paidOn;
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "实际付款金额必须大于 0。");
        Amount = decimal.Round(amount, 2);
        Currency = Required(currency, "币种").ToUpperInvariant();
        if (Currency.Length is < 3 or > 10) throw new ArgumentException("币种长度必须在 3 到 10 个字符之间。", nameof(currency));
        Channel = channel;
        ExternalReference = Required(externalReference, "外部付款参考号");
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        ErpSettlementId = erpSettlementId;
        Operator = Required(@operator, "付款登记人");
        CreatedAt = createdAt;
    }

    public static OaPaymentExecution Restore(Guid id, Guid paymentRequestId, string executionNo, DateOnly paidOn, decimal amount,
        string currency, OaPaymentExecutionChannel channel, string externalReference, string? notes, Guid? erpSettlementId,
        string @operator, DateTime createdAt)
    {
        var item = new OaPaymentExecution(paymentRequestId, executionNo, paidOn, amount, currency, channel,
            externalReference, notes, erpSettlementId, @operator, createdAt);
        item.Id = id;
        return item;
    }

    private static string Required(string? value, string label)
        => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{label}不能为空。") : value.Trim();
}
