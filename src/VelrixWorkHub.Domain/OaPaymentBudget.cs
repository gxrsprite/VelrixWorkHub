namespace VelrixWorkHub.Domain;

public enum OaPaymentBudgetStatus
{
    Active,
    Closed
}

public enum OaPaymentBudgetReservationStatus
{
    Reserved,
    Consumed,
    Released
}

public sealed class OaPaymentBudget
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string BudgetNo { get; private set; } = string.Empty;
    public string LegalEntity { get; private set; } = string.Empty;
    public string DepartmentName { get; private set; } = string.Empty;
    public string Currency { get; private set; } = "CNY";
    public decimal TotalAmount { get; private set; }
    public decimal ReservedAmount { get; private set; }
    public decimal ConsumedAmount { get; private set; }
    public OaPaymentBudgetStatus Status { get; private set; } = OaPaymentBudgetStatus.Active;
    public string OtherInfo { get; private set; } = "{}";
    public DateTime CreatedAt { get; private set; }
    public decimal AvailableAmount => decimal.Round(TotalAmount - ReservedAmount - ConsumedAmount, 2);

    public OaPaymentBudget(string budgetNo, string legalEntity, string departmentName, string currency, decimal totalAmount,
        string? otherInfo, DateTime createdAt)
    {
        BudgetNo = Required(budgetNo, "预算编号");
        LegalEntity = Required(legalEntity, "主体公司");
        DepartmentName = Required(departmentName, "部门");
        Currency = Required(currency, "币种").ToUpperInvariant();
        if (Currency.Length is < 3 or > 10) throw new ArgumentException("币种长度必须在 3 到 10 个字符之间。", nameof(currency));
        if (totalAmount <= 0) throw new ArgumentOutOfRangeException(nameof(totalAmount), "预算总额必须大于 0。");
        TotalAmount = decimal.Round(totalAmount, 2);
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
        CreatedAt = createdAt;
    }

    public static OaPaymentBudget Restore(Guid id, string budgetNo, string legalEntity, string departmentName, string currency,
        decimal totalAmount, decimal reservedAmount, decimal consumedAmount, OaPaymentBudgetStatus status, string? otherInfo, DateTime createdAt)
    {
        var item = new OaPaymentBudget(budgetNo, legalEntity, departmentName, currency, totalAmount, otherInfo, createdAt)
        {
            Id = id,
            ReservedAmount = decimal.Round(reservedAmount, 2),
            ConsumedAmount = decimal.Round(consumedAmount, 2),
            Status = status
        };
        if (item.ReservedAmount < 0 || item.ConsumedAmount < 0 || item.AvailableAmount < 0)
            throw new InvalidOperationException("预算余额数据无效。");
        return item;
    }

    public void Reserve(decimal amount)
    {
        EnsureActive();
        amount = NormalizeAmount(amount);
        if (amount > AvailableAmount) throw new InvalidOperationException($"预算可用余额不足，当前可用 {AvailableAmount:N2}。");
        ReservedAmount += amount;
    }

    public void Release(decimal amount)
    {
        amount = NormalizeAmount(amount);
        if (amount > ReservedAmount) throw new InvalidOperationException("预算待占用金额不足，不能释放。");
        ReservedAmount -= amount;
    }

    public void Consume(decimal amount)
    {
        amount = NormalizeAmount(amount);
        if (amount > ReservedAmount) throw new InvalidOperationException("预算未完成占用，不能转为已执行金额。");
        ReservedAmount -= amount;
        ConsumedAmount += amount;
    }

    public void Close()
    {
        if (Status == OaPaymentBudgetStatus.Closed) return;
        if (ReservedAmount > 0) throw new InvalidOperationException("仍有付款申请占用预算，不能关闭预算。");
        Status = OaPaymentBudgetStatus.Closed;
    }

    private void EnsureActive()
    {
        if (Status != OaPaymentBudgetStatus.Active) throw new InvalidOperationException("预算已关闭，不能继续占用。");
    }

    private static decimal NormalizeAmount(decimal amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "预算金额必须大于 0。");
        return decimal.Round(amount, 2);
    }

    private static string Required(string? value, string label)
        => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{label}不能为空。") : value.Trim();
}

public sealed class OaPaymentBudgetReservation
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid BudgetId { get; private set; }
    public Guid PaymentRequestId { get; private set; }
    public decimal Amount { get; private set; }
    public OaPaymentBudgetReservationStatus Status { get; private set; } = OaPaymentBudgetReservationStatus.Reserved;
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public OaPaymentBudgetReservation(Guid budgetId, Guid paymentRequestId, decimal amount, DateTime createdAt)
    {
        if (budgetId == Guid.Empty) throw new ArgumentException("预算不能为空。", nameof(budgetId));
        if (paymentRequestId == Guid.Empty) throw new ArgumentException("付款申请不能为空。", nameof(paymentRequestId));
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "预算占用金额必须大于 0。");
        BudgetId = budgetId;
        PaymentRequestId = paymentRequestId;
        Amount = decimal.Round(amount, 2);
        CreatedAt = createdAt;
    }

    public static OaPaymentBudgetReservation Restore(Guid id, Guid budgetId, Guid paymentRequestId, decimal amount,
        OaPaymentBudgetReservationStatus status, DateTime createdAt, DateTime? completedAt)
    {
        var item = new OaPaymentBudgetReservation(budgetId, paymentRequestId, amount, createdAt)
        {
            Id = id,
            Status = status,
            CompletedAt = completedAt
        };
        return item;
    }

    public void Release(DateTime? completedAt = null)
    {
        if (Status != OaPaymentBudgetReservationStatus.Reserved) return;
        Status = OaPaymentBudgetReservationStatus.Released;
        CompletedAt = completedAt ?? DateTime.Now;
    }

    public void Consume(DateTime? completedAt = null)
    {
        if (Status != OaPaymentBudgetReservationStatus.Reserved) throw new InvalidOperationException("预算占用已结束，不能重复执行。");
        Status = OaPaymentBudgetReservationStatus.Consumed;
        CompletedAt = completedAt ?? DateTime.Now;
    }

    public void ReserveAgain(decimal amount)
    {
        if (Status != OaPaymentBudgetReservationStatus.Released) throw new InvalidOperationException("只有已释放的预算占用才能重新激活。");
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "预算占用金额必须大于 0。");
        Amount = decimal.Round(amount, 2);
        Status = OaPaymentBudgetReservationStatus.Reserved;
        CompletedAt = null;
    }
}
