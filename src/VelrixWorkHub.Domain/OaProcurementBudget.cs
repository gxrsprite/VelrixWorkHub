namespace VelrixWorkHub.Domain;

public enum OaProcurementBudgetStatus
{
    Active,
    Closed
}

public enum OaProcurementBudgetReservationStatus
{
    Reserved,
    Consumed,
    Released
}

public sealed class OaProcurementBudget
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string BudgetNo { get; private set; } = string.Empty;
    public string LegalEntity { get; private set; } = string.Empty;
    public string DepartmentName { get; private set; } = string.Empty;
    public decimal TotalAmount { get; private set; }
    public decimal ReservedAmount { get; private set; }
    public decimal ConsumedAmount { get; private set; }
    public OaProcurementBudgetStatus Status { get; private set; } = OaProcurementBudgetStatus.Active;
    public string OtherInfo { get; private set; } = "{}";
    public DateTime CreatedAt { get; private set; }
    public decimal AvailableAmount => decimal.Round(TotalAmount - ReservedAmount - ConsumedAmount, 2);

    public OaProcurementBudget(string budgetNo, string legalEntity, string departmentName, decimal totalAmount,
        string? otherInfo, DateTime createdAt)
    {
        BudgetNo = Required(budgetNo, "预算编号");
        LegalEntity = Required(legalEntity, "主体公司");
        DepartmentName = Required(departmentName, "部门");
        if (totalAmount <= 0) throw new ArgumentOutOfRangeException(nameof(totalAmount), "预算总额必须大于 0。");
        TotalAmount = decimal.Round(totalAmount, 2);
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
        CreatedAt = createdAt;
    }

    public static OaProcurementBudget Restore(Guid id, string budgetNo, string legalEntity, string departmentName,
        decimal totalAmount, decimal reservedAmount, decimal consumedAmount, OaProcurementBudgetStatus status,
        string? otherInfo, DateTime createdAt)
    {
        var item = new OaProcurementBudget(budgetNo, legalEntity, departmentName, totalAmount, otherInfo, createdAt)
        {
            Id = id,
            ReservedAmount = decimal.Round(reservedAmount, 2),
            ConsumedAmount = decimal.Round(consumedAmount, 2),
            Status = status
        };
        if (item.ReservedAmount < 0 || item.ConsumedAmount < 0 || item.AvailableAmount < 0)
            throw new InvalidOperationException("采购预算余额数据无效。");
        return item;
    }

    public void Reserve(decimal amount)
    {
        EnsureActive();
        amount = NormalizeAmount(amount);
        if (amount > AvailableAmount) throw new InvalidOperationException($"采购预算可用余额不足，当前可用 {AvailableAmount:N2}。");
        ReservedAmount += amount;
    }

    public void Release(decimal amount)
    {
        amount = NormalizeAmount(amount);
        if (amount > ReservedAmount) throw new InvalidOperationException("采购预算待占用金额不足，不能释放。");
        ReservedAmount -= amount;
    }

    public void Consume(decimal amount)
    {
        amount = NormalizeAmount(amount);
        if (amount > ReservedAmount) throw new InvalidOperationException("采购预算未完成占用，不能转为已执行金额。");
        ReservedAmount -= amount;
        ConsumedAmount += amount;
    }

    public void RestoreConsumed(decimal amount)
    {
        amount = NormalizeAmount(amount);
        if (amount > ConsumedAmount) throw new InvalidOperationException("采购预算已执行金额不足，不能恢复。");
        ConsumedAmount -= amount;
    }

    public void Close()
    {
        if (Status == OaProcurementBudgetStatus.Closed) return;
        if (ReservedAmount > 0) throw new InvalidOperationException("仍有采购申请占用预算，不能关闭预算。");
        Status = OaProcurementBudgetStatus.Closed;
    }

    private void EnsureActive()
    {
        if (Status != OaProcurementBudgetStatus.Active) throw new InvalidOperationException("采购预算已关闭，不能继续占用。");
    }

    private static decimal NormalizeAmount(decimal amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "预算金额必须大于 0。");
        return decimal.Round(amount, 2);
    }

    private static string Required(string? value, string label)
        => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{label}不能为空。") : value.Trim();
}

public sealed class OaProcurementBudgetReservation
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid BudgetId { get; private set; }
    public Guid ProcurementRequestId { get; private set; }
    public decimal Amount { get; private set; }
    public OaProcurementBudgetReservationStatus Status { get; private set; } = OaProcurementBudgetReservationStatus.Reserved;
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public OaProcurementBudgetReservation(Guid budgetId, Guid procurementRequestId, decimal amount, DateTime createdAt)
    {
        if (budgetId == Guid.Empty) throw new ArgumentException("预算不能为空。", nameof(budgetId));
        if (procurementRequestId == Guid.Empty) throw new ArgumentException("采购申请不能为空。", nameof(procurementRequestId));
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "预算占用金额必须大于 0。");
        BudgetId = budgetId;
        ProcurementRequestId = procurementRequestId;
        Amount = decimal.Round(amount, 2);
        CreatedAt = createdAt;
    }

    public static OaProcurementBudgetReservation Restore(Guid id, Guid budgetId, Guid procurementRequestId,
        decimal amount, OaProcurementBudgetReservationStatus status, DateTime createdAt, DateTime? completedAt)
    {
        var item = new OaProcurementBudgetReservation(budgetId, procurementRequestId, amount, createdAt)
        {
            Id = id,
            Status = status,
            CompletedAt = completedAt
        };
        return item;
    }

    public void Release(DateTime? completedAt = null)
    {
        if (Status != OaProcurementBudgetReservationStatus.Reserved) return;
        Status = OaProcurementBudgetReservationStatus.Released;
        CompletedAt = completedAt ?? DateTime.Now;
    }

    public void Consume(DateTime? completedAt = null)
    {
        if (Status != OaProcurementBudgetReservationStatus.Reserved) throw new InvalidOperationException("采购预算占用已结束，不能重复执行。");
        Status = OaProcurementBudgetReservationStatus.Consumed;
        CompletedAt = completedAt ?? DateTime.Now;
    }

    public void ReleaseConsumed(DateTime? completedAt = null)
    {
        if (Status != OaProcurementBudgetReservationStatus.Consumed) return;
        Status = OaProcurementBudgetReservationStatus.Released;
        CompletedAt = completedAt ?? DateTime.Now;
    }

    public void ReserveAgain(decimal amount)
    {
        if (Status != OaProcurementBudgetReservationStatus.Released) throw new InvalidOperationException("只有已释放的采购预算占用才能重新激活。");
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "预算占用金额必须大于 0。");
        Amount = decimal.Round(amount, 2);
        Status = OaProcurementBudgetReservationStatus.Reserved;
        CompletedAt = null;
    }
}
