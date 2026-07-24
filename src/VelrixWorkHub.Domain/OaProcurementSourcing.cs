namespace VelrixWorkHub.Domain;

public enum OaProcurementSourcingStatus
{
    Draft,
    Submitted,
    Awarded,
    Cancelled
}

public sealed class OaProcurementSourcing
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string SourcingNo { get; private set; } = string.Empty;
    public Guid ProcurementRequestId { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public string OtherInfo { get; private set; } = "{}";
    public OaProcurementSourcingStatus Status { get; private set; } = OaProcurementSourcingStatus.Draft;
    public Guid? AwardedQuoteId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? AwardedAt { get; private set; }

    public OaProcurementSourcing(string sourcingNo, Guid procurementRequestId, string createdBy, string? otherInfo, DateTime createdAt)
    {
        SourcingNo = Required(sourcingNo, "寻源编号");
        if (procurementRequestId == Guid.Empty) throw new ArgumentException("采购申请不能为空。", nameof(procurementRequestId));
        ProcurementRequestId = procurementRequestId;
        CreatedBy = Required(createdBy, "创建人");
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
        CreatedAt = createdAt;
    }

    public static OaProcurementSourcing Restore(Guid id, string sourcingNo, Guid procurementRequestId, string createdBy,
        string? otherInfo, OaProcurementSourcingStatus status, Guid? awardedQuoteId, DateTime createdAt, DateTime? awardedAt)
    {
        var item = new OaProcurementSourcing(sourcingNo, procurementRequestId, createdBy, otherInfo, createdAt)
        {
            Id = id,
            Status = status,
            AwardedQuoteId = awardedQuoteId,
            AwardedAt = awardedAt
        };
        return item;
    }

    public void Submit(int quoteCount)
    {
        if (Status != OaProcurementSourcingStatus.Draft) throw new InvalidOperationException("只有草稿寻源单可以提交比价。");
        if (quoteCount < 2) throw new InvalidOperationException("寻源比价至少需要两家供应商报价。");
        Status = OaProcurementSourcingStatus.Submitted;
    }

    public void Award(Guid quoteId, DateTime? awardedAt = null)
    {
        if (Status != OaProcurementSourcingStatus.Submitted) throw new InvalidOperationException("只有已提交寻源单可以选择中选报价。");
        if (quoteId == Guid.Empty) throw new ArgumentException("中选报价不能为空。", nameof(quoteId));
        AwardedQuoteId = quoteId;
        AwardedAt = awardedAt ?? DateTime.Now;
        Status = OaProcurementSourcingStatus.Awarded;
    }

    public void Cancel()
    {
        if (Status == OaProcurementSourcingStatus.Cancelled) return;
        if (Status == OaProcurementSourcingStatus.Awarded) throw new InvalidOperationException("已选择中选报价的寻源单不能直接撤销。");
        if (Status is not (OaProcurementSourcingStatus.Draft or OaProcurementSourcingStatus.Submitted)) throw new InvalidOperationException("当前寻源单不能撤销。");
        Status = OaProcurementSourcingStatus.Cancelled;
    }

    private static string Required(string? value, string label)
        => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{label}不能为空。") : value.Trim();
}

public sealed class OaProcurementSourcingQuote
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid SourcingId { get; private set; }
    public Guid SupplierId { get; private set; }
    public decimal QuoteAmount { get; private set; }
    public int DeliveryDays { get; private set; }
    public DateOnly ValidUntil { get; private set; }
    public string? Notes { get; private set; }
    public string OtherInfo { get; private set; } = "{}";
    public DateTime CreatedAt { get; private set; }

    public OaProcurementSourcingQuote(Guid sourcingId, Guid supplierId, decimal quoteAmount, int deliveryDays,
        DateOnly validUntil, string? notes, string? otherInfo, DateTime createdAt)
    {
        if (sourcingId == Guid.Empty) throw new ArgumentException("寻源单不能为空。", nameof(sourcingId));
        if (supplierId == Guid.Empty) throw new ArgumentException("供应商不能为空。", nameof(supplierId));
        if (quoteAmount <= 0) throw new ArgumentOutOfRangeException(nameof(quoteAmount), "报价金额必须大于 0。");
        if (deliveryDays < 0) throw new ArgumentOutOfRangeException(nameof(deliveryDays), "交付天数不能为负数。");
        if (validUntil == default) throw new ArgumentException("报价有效期不能为空。", nameof(validUntil));
        SourcingId = sourcingId;
        SupplierId = supplierId;
        QuoteAmount = decimal.Round(quoteAmount, 2);
        DeliveryDays = deliveryDays;
        ValidUntil = validUntil;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
        CreatedAt = createdAt;
    }

    public static OaProcurementSourcingQuote Restore(Guid id, Guid sourcingId, Guid supplierId, decimal quoteAmount,
        int deliveryDays, DateOnly validUntil, string? notes, string? otherInfo, DateTime createdAt)
    {
        var item = new OaProcurementSourcingQuote(sourcingId, supplierId, quoteAmount, deliveryDays, validUntil, notes, otherInfo, createdAt) { Id = id };
        return item;
    }
}
