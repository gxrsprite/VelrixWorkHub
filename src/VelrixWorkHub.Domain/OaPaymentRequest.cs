namespace VelrixWorkHub.Domain;

public enum OaPaymentRequestType
{
    SupplierPayment,
    EmployeePayment,
    AdvanceSettlement,
    Other
}

public enum OaPaymentRequestStatus
{
    Draft,
    Submitted,
    Rejected,
    Approved,
    Paid,
    Cancelled
}

public enum OaPaymentFinanceReviewStatus
{
    Pending,
    Approved,
    Rejected
}

/// <summary>OA 付款意图申请，不代表 ERP 付款流水或银行直连结果。</summary>
public sealed class OaPaymentRequest
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string DocumentNo { get; private set; } = string.Empty;
    public Guid ApplicantUserId { get; init; }
    public string ApplicantName { get; private set; } = string.Empty;
    public string DepartmentName { get; private set; } = string.Empty;
    public string LegalEntity { get; private set; } = string.Empty;
    public string PayeeName { get; private set; } = string.Empty;
    public string PayeeAccountReference { get; private set; } = string.Empty;
    public string PaymentBankName { get; private set; } = string.Empty;
    public string Currency { get; private set; } = "CNY";
    public decimal Amount { get; private set; }
    public OaPaymentRequestType PaymentType { get; private set; }
    public DateOnly RequestDate { get; private set; }
    public DateOnly RequestedPaymentDate { get; private set; }
    public string? PrecedingDocumentNo { get; private set; }
    public string? BudgetReference { get; private set; }
    public Guid? ProjectId { get; private set; }
    public string Purpose { get; private set; } = string.Empty;
    public string OtherInfo { get; private set; } = "{}";
    public OaPaymentRequestStatus Status { get; private set; }
    public OaPaymentFinanceReviewStatus FinanceReviewStatus { get; private set; } = OaPaymentFinanceReviewStatus.Pending;
    public string? FinanceReviewReason { get; private set; }
    public string? FinanceReviewer { get; private set; }
    public DateTime? FinanceReviewedAt { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? SubmittedAt { get; private set; }

    public OaPaymentRequest(Guid applicantUserId, string applicantName, string departmentName, string legalEntity,
        string documentNo, string payeeName, string payeeAccountReference, string paymentBankName, string currency,
        decimal amount, OaPaymentRequestType paymentType, DateOnly requestDate, DateOnly requestedPaymentDate,
        string? precedingDocumentNo, Guid? projectId, string purpose, string? otherInfo, DateTime createdAt, string? budgetReference = null)
    {
        if (applicantUserId == Guid.Empty) throw new ArgumentException("申请人不能为空。", nameof(applicantUserId));
        ApplicantUserId = applicantUserId;
        CreatedAt = createdAt;
        Edit(applicantName, departmentName, legalEntity, documentNo, payeeName, payeeAccountReference, paymentBankName, currency,
            amount, paymentType, requestDate, requestedPaymentDate, precedingDocumentNo, projectId, purpose, otherInfo);
        BudgetReference = Clean(budgetReference);
        Status = OaPaymentRequestStatus.Draft;
    }

    public void Edit(string applicantName, string departmentName, string legalEntity, string documentNo, string payeeName,
        string payeeAccountReference, string paymentBankName, string currency, decimal amount, OaPaymentRequestType paymentType,
        DateOnly requestDate, DateOnly requestedPaymentDate, string? precedingDocumentNo, Guid? projectId, string purpose, string? otherInfo)
    {
        DocumentNo = Required(documentNo, "付款申请单号");
        ApplicantName = Required(applicantName, "申请人");
        DepartmentName = Required(departmentName, "申请部门");
        LegalEntity = Required(legalEntity, "主体公司");
        PayeeName = Required(payeeName, "收款方");
        PayeeAccountReference = Required(payeeAccountReference, "收款账户引用");
        PaymentBankName = Required(paymentBankName, "收款银行");
        Currency = Required(currency, "币种").ToUpperInvariant();
        if (Currency.Length is < 3 or > 10) throw new ArgumentException("币种长度必须在 3 到 10 个字符之间。", nameof(currency));
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "付款金额必须大于 0。");
        if (requestDate == default) throw new ArgumentException("申请日期不能为空。", nameof(requestDate));
        if (requestedPaymentDate < requestDate) throw new ArgumentException("期望付款日期不能早于申请日期。", nameof(requestedPaymentDate));
        if (string.IsNullOrWhiteSpace(purpose)) throw new ArgumentException("付款事由不能为空。", nameof(purpose));
        RequestDate = requestDate;
        RequestedPaymentDate = requestedPaymentDate;
        Amount = decimal.Round(amount, 2);
        PaymentType = paymentType;
        PrecedingDocumentNo = Clean(precedingDocumentNo);
        ProjectId = projectId;
        Purpose = purpose.Trim();
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public void SetBudgetReference(string? budgetReference) => BudgetReference = Clean(budgetReference);

    public void Submit(DateTime submittedAt)
    {
        if (Status is not (OaPaymentRequestStatus.Draft or OaPaymentRequestStatus.Rejected)) throw new InvalidOperationException("只有草稿或已驳回付款申请才能提交。");
        Status = OaPaymentRequestStatus.Submitted;
        RejectionReason = null;
        FinanceReviewStatus = OaPaymentFinanceReviewStatus.Pending;
        FinanceReviewReason = null;
        FinanceReviewer = null;
        FinanceReviewedAt = null;
        SubmittedAt = submittedAt;
    }

    public void Approve()
    {
        if (Status != OaPaymentRequestStatus.Submitted) throw new InvalidOperationException("只有已提交付款申请才能批准。");
        Status = OaPaymentRequestStatus.Approved;
    }

    public void Reject(string? reason = null)
    {
        if (Status != OaPaymentRequestStatus.Submitted) throw new InvalidOperationException("只有已提交付款申请才能驳回。");
        Status = OaPaymentRequestStatus.Rejected;
        RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    public void Cancel()
    {
        if (Status is not (OaPaymentRequestStatus.Draft or OaPaymentRequestStatus.Submitted)) throw new InvalidOperationException("当前状态不能撤回付款申请。");
        Status = OaPaymentRequestStatus.Cancelled;
    }

    public void ReviewFinance(string reviewer, bool approved, string? reason = null, DateTime? reviewedAt = null)
    {
        if (Status != OaPaymentRequestStatus.Approved) throw new InvalidOperationException("只有已批准付款申请才能进行财务复核。");
        if (FinanceReviewStatus != OaPaymentFinanceReviewStatus.Pending) throw new InvalidOperationException("该付款申请已完成财务复核。");
        if (string.IsNullOrWhiteSpace(reviewer)) throw new ArgumentException("财务复核人不能为空。", nameof(reviewer));
        if (!approved && string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("财务复核不通过时必须填写原因。", nameof(reason));

        FinanceReviewStatus = approved ? OaPaymentFinanceReviewStatus.Approved : OaPaymentFinanceReviewStatus.Rejected;
        FinanceReviewReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        FinanceReviewer = reviewer.Trim();
        FinanceReviewedAt = reviewedAt ?? DateTime.Now;
        if (!approved) RejectionReason = FinanceReviewReason;
    }

    /// <summary>仅供后续财务付款用例或持久化恢复使用。</summary>
    public void MarkPaid()
    {
        if (Status != OaPaymentRequestStatus.Approved) throw new InvalidOperationException("只有已批准付款申请才能登记付款完成。");
        if (FinanceReviewStatus != OaPaymentFinanceReviewStatus.Approved) throw new InvalidOperationException("只有财务复核通过的付款申请才能登记付款完成。");
        Status = OaPaymentRequestStatus.Paid;
    }

    public void SetStatus(OaPaymentRequestStatus status) => Status = status;

    public void RestoreFinanceReviewForRecovery(OaPaymentFinanceReviewStatus status, string? reason, string? reviewer,
        DateTime? reviewedAt, string? rejectionReason)
    {
        FinanceReviewStatus = status;
        FinanceReviewReason = reason;
        FinanceReviewer = reviewer;
        FinanceReviewedAt = reviewedAt;
        RejectionReason = rejectionReason;
    }

    private static string Required(string? value, string label) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{label}不能为空。") : value.Trim();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
