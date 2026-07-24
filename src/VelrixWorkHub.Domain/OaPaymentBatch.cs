namespace VelrixWorkHub.Domain;

public enum OaPaymentBatchStatus
{
    Draft,
    Submitted,
    Cancelled
}

public sealed class OaPaymentBatch
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string BatchNo { get; private set; } = string.Empty;
    public DateOnly PaymentDate { get; private set; }
    public string Currency { get; private set; } = "CNY";
    public decimal TotalAmount { get; private set; }
    public int ItemCount { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public string OtherInfo { get; private set; } = "{}";
    public OaPaymentBatchStatus Status { get; private set; } = OaPaymentBatchStatus.Draft;
    public DateTime CreatedAt { get; private set; }

    public OaPaymentBatch(string batchNo, DateOnly paymentDate, string currency, string createdBy, string? otherInfo, DateTime createdAt)
    {
        BatchNo = Required(batchNo, "付款批次号");
        if (paymentDate == default) throw new ArgumentException("批次付款日期不能为空。", nameof(paymentDate));
        PaymentDate = paymentDate;
        Currency = Required(currency, "币种").ToUpperInvariant();
        if (Currency.Length is < 3 or > 10) throw new ArgumentException("币种长度必须在 3 到 10 个字符之间。", nameof(currency));
        CreatedBy = Required(createdBy, "创建人");
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
        CreatedAt = createdAt;
    }

    public static OaPaymentBatch Restore(Guid id, string batchNo, DateOnly paymentDate, string currency, decimal totalAmount,
        int itemCount, string createdBy, string? otherInfo, OaPaymentBatchStatus status, DateTime createdAt)
    {
        var item = new OaPaymentBatch(batchNo, paymentDate, currency, createdBy, otherInfo, createdAt)
        {
            Id = id,
            TotalAmount = decimal.Round(totalAmount, 2),
            ItemCount = itemCount,
            Status = status
        };
        if (item.TotalAmount < 0 || item.ItemCount < 0) throw new InvalidOperationException("付款批次汇总数据无效。");
        return item;
    }

    public void Add(decimal amount)
    {
        EnsureDraft();
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "批次明细金额必须大于 0。");
        TotalAmount += decimal.Round(amount, 2);
        ItemCount++;
    }

    public void Remove(decimal amount)
    {
        EnsureDraft();
        if (amount <= 0 || ItemCount <= 0 || amount > TotalAmount) throw new InvalidOperationException("付款批次明细汇总无效。");
        TotalAmount -= decimal.Round(amount, 2);
        ItemCount--;
    }

    public void Submit()
    {
        EnsureDraft();
        if (ItemCount == 0) throw new InvalidOperationException("付款批次至少需要一条付款申请。");
        Status = OaPaymentBatchStatus.Submitted;
    }

    public void Cancel()
    {
        if (Status == OaPaymentBatchStatus.Cancelled) return;
        if (Status is not (OaPaymentBatchStatus.Draft or OaPaymentBatchStatus.Submitted)) throw new InvalidOperationException("当前付款批次不能撤回。");
        Status = OaPaymentBatchStatus.Cancelled;
    }

    private void EnsureDraft()
    {
        if (Status != OaPaymentBatchStatus.Draft) throw new InvalidOperationException("只有草稿付款批次可以调整明细。");
    }

    private static string Required(string? value, string label)
        => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{label}不能为空。") : value.Trim();
}

public sealed class OaPaymentBatchItem
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid BatchId { get; private set; }
    public Guid PaymentRequestId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public OaPaymentBatchItem(Guid batchId, Guid paymentRequestId, decimal amount, DateTime createdAt)
    {
        if (batchId == Guid.Empty) throw new ArgumentException("付款批次不能为空。", nameof(batchId));
        if (paymentRequestId == Guid.Empty) throw new ArgumentException("付款申请不能为空。", nameof(paymentRequestId));
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "批次明细金额必须大于 0。");
        BatchId = batchId;
        PaymentRequestId = paymentRequestId;
        Amount = decimal.Round(amount, 2);
        CreatedAt = createdAt;
    }

    public static OaPaymentBatchItem Restore(Guid id, Guid batchId, Guid paymentRequestId, decimal amount, DateTime createdAt)
    {
        var item = new OaPaymentBatchItem(batchId, paymentRequestId, amount, createdAt) { Id = id };
        return item;
    }
}
