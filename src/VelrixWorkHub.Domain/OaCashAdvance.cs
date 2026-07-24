namespace VelrixWorkHub.Domain;

public enum OaCashAdvanceType
{
    Temporary,
    Travel,
    PettyCash,
    Other
}

public enum OaCashAdvanceStatus
{
    Draft,
    Submitted,
    Rejected,
    Approved,
    PartiallySettled,
    Settled,
    Cancelled
}

/// <summary>OA 借款/备用金申请。付款本身由后续财务用例负责，不等同于 ERP 付款流水。</summary>
public sealed class OaCashAdvance
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string DocumentNo { get; private set; } = string.Empty;
    public Guid ApplicantUserId { get; init; }
    public string ApplicantName { get; private set; } = string.Empty;
    public string DepartmentName { get; private set; } = string.Empty;
    public string LegalEntity { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public OaCashAdvanceType AdvanceType { get; private set; }
    public DateOnly RequestDate { get; private set; }
    public DateOnly ExpectedSettlementDate { get; private set; }
    public Guid? ProjectId { get; private set; }
    public decimal Amount { get; private set; }
    /// <summary>来自已批准报销冲销和已批准还款的累计结清金额。</summary>
    public decimal SettledAmount { get; private set; }
    public decimal RemainingAmount => decimal.Round(Amount - SettledAmount, 2);
    public string Purpose { get; private set; } = string.Empty;
    public string OtherInfo { get; private set; } = "{}";
    public OaCashAdvanceStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? SubmittedAt { get; private set; }

    public OaCashAdvance(Guid applicantUserId, string applicantName, string departmentName, string legalEntity,
        string documentNo, string title, OaCashAdvanceType advanceType, DateOnly requestDate,
        DateOnly expectedSettlementDate, Guid? projectId, decimal amount, string purpose, string? otherInfo, DateTime createdAt)
    {
        if (applicantUserId == Guid.Empty) throw new ArgumentException("申请人不能为空。", nameof(applicantUserId));
        ApplicantUserId = applicantUserId;
        CreatedAt = createdAt;
        Edit(applicantName, departmentName, legalEntity, documentNo, title, advanceType, requestDate, expectedSettlementDate,
            projectId, amount, purpose, otherInfo);
        Status = OaCashAdvanceStatus.Draft;
    }

    public void Edit(string applicantName, string departmentName, string legalEntity, string documentNo, string title,
        OaCashAdvanceType advanceType, DateOnly requestDate, DateOnly expectedSettlementDate, Guid? projectId,
        decimal amount, string purpose, string? otherInfo)
    {
        DocumentNo = Required(documentNo, "借款单号");
        ApplicantName = Required(applicantName, "申请人");
        DepartmentName = Required(departmentName, "申请部门");
        LegalEntity = Required(legalEntity, "主体公司");
        Title = Required(title, "借款标题");
        if (requestDate == default) throw new ArgumentException("申请日期不能为空。", nameof(requestDate));
        if (expectedSettlementDate < requestDate) throw new ArgumentException("预计冲销日期不能早于申请日期。", nameof(expectedSettlementDate));
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "借款金额必须大于 0。");
        if (SettledAmount > amount) throw new InvalidOperationException("借款金额不能低于已冲销金额。");
        if (string.IsNullOrWhiteSpace(purpose)) throw new ArgumentException("借款用途不能为空。", nameof(purpose));
        RequestDate = requestDate;
        ExpectedSettlementDate = expectedSettlementDate;
        AdvanceType = advanceType;
        ProjectId = projectId;
        Amount = decimal.Round(amount, 2);
        Purpose = purpose.Trim();
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public void Submit(DateTime submittedAt)
    {
        if (Status is not (OaCashAdvanceStatus.Draft or OaCashAdvanceStatus.Rejected)) throw new InvalidOperationException("只有草稿或已驳回借款才能提交。");
        Status = OaCashAdvanceStatus.Submitted;
        RejectionReason = null;
        SubmittedAt = submittedAt;
    }

    public void Approve()
    {
        if (Status != OaCashAdvanceStatus.Submitted) throw new InvalidOperationException("只有已提交的借款才能批准。");
        Status = OaCashAdvanceStatus.Approved;
    }

    public void Reject(string? reason = null)
    {
        if (Status != OaCashAdvanceStatus.Submitted) throw new InvalidOperationException("只有已提交的借款才能驳回。");
        Status = OaCashAdvanceStatus.Rejected;
        RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    public void Cancel()
    {
        if (Status is not (OaCashAdvanceStatus.Draft or OaCashAdvanceStatus.Submitted)) throw new InvalidOperationException("当前状态不能撤回借款。");
        Status = OaCashAdvanceStatus.Cancelled;
    }

    public void ApplyOffset(decimal amount) => ApplySettlement(amount);

    public void ApplySettlement(decimal amount)
    {
        if (Status is not (OaCashAdvanceStatus.Approved or OaCashAdvanceStatus.PartiallySettled)) throw new InvalidOperationException("只有已批准或部分结清的借款才能结清。 ");
        if (amount <= 0 || amount > RemainingAmount) throw new ArgumentOutOfRangeException(nameof(amount), "结清金额必须大于 0 且不能超过借款余额。 ");
        SettledAmount = decimal.Round(SettledAmount + amount, 2);
        Status = RemainingAmount == 0 ? OaCashAdvanceStatus.Settled : OaCashAdvanceStatus.PartiallySettled;
    }

    /// <summary>仅供事务失败恢复或持久化重建使用。</summary>
    public void SetStatus(OaCashAdvanceStatus status) => Status = status;

    public void SetSettledAmount(decimal amount)
    {
        if (amount < 0 || amount > Amount) throw new ArgumentOutOfRangeException(nameof(amount));
        SettledAmount = decimal.Round(amount, 2);
    }

    private static string Required(string? value, string label) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{label}不能为空。") : value.Trim();
}

public sealed class OaCashAdvanceOffset
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid CashAdvanceId { get; init; }
    public Guid ReimbursementId { get; init; }
    public decimal Amount { get; private set; }
    public DateOnly OffsetDate { get; private set; }
    public string Notes { get; private set; } = string.Empty;
    public string OtherInfo { get; private set; } = "{}";

    public OaCashAdvanceOffset(Guid cashAdvanceId, Guid reimbursementId, decimal amount, DateOnly offsetDate, string notes, string? otherInfo)
    {
        if (cashAdvanceId == Guid.Empty) throw new ArgumentException("借款单不能为空。", nameof(cashAdvanceId));
        if (reimbursementId == Guid.Empty) throw new ArgumentException("报销单不能为空。", nameof(reimbursementId));
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "冲销金额必须大于 0。");
        if (offsetDate == default) throw new ArgumentException("冲销日期不能为空。", nameof(offsetDate));
        if (string.IsNullOrWhiteSpace(notes)) throw new ArgumentException("冲销说明不能为空。", nameof(notes));
        CashAdvanceId = cashAdvanceId;
        ReimbursementId = reimbursementId;
        Amount = decimal.Round(amount, 2);
        OffsetDate = offsetDate;
        Notes = notes.Trim();
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }
}
