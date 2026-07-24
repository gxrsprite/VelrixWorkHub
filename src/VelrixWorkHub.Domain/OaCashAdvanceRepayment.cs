namespace VelrixWorkHub.Domain;

public enum OaCashAdvanceRepaymentMethod
{
    Cash,
    BankTransfer,
    Other
}

public enum OaCashAdvanceRepaymentStatus
{
    Draft,
    Submitted,
    Rejected,
    Approved,
    Cancelled
}

/// <summary>OA 借款还款申请。审批通过后才计入借款已结清金额，不生成 ERP 或银行交易。</summary>
public sealed class OaCashAdvanceRepayment
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid CashAdvanceId { get; init; }
    public Guid ApplicantUserId { get; init; }
    public string ApplicantName { get; private set; } = string.Empty;
    public string DepartmentName { get; private set; } = string.Empty;
    public string LegalEntity { get; private set; } = string.Empty;
    public string DocumentNo { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public DateOnly RepaymentDate { get; private set; }
    public OaCashAdvanceRepaymentMethod RepaymentMethod { get; private set; }
    /// <summary>受控收款信息或回单引用，禁止保存完整银行卡号。</summary>
    public string ReceiptReference { get; private set; } = string.Empty;
    public string Notes { get; private set; } = string.Empty;
    public string OtherInfo { get; private set; } = "{}";
    public OaCashAdvanceRepaymentStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? SubmittedAt { get; private set; }

    public OaCashAdvanceRepayment(Guid cashAdvanceId, Guid applicantUserId, string applicantName, string departmentName,
        string legalEntity, string documentNo, string title, decimal amount, DateOnly repaymentDate,
        OaCashAdvanceRepaymentMethod repaymentMethod, string receiptReference, string notes, string? otherInfo, DateTime createdAt)
    {
        if (cashAdvanceId == Guid.Empty) throw new ArgumentException("前置借款不能为空。", nameof(cashAdvanceId));
        if (applicantUserId == Guid.Empty) throw new ArgumentException("申请人不能为空。", nameof(applicantUserId));
        CashAdvanceId = cashAdvanceId;
        ApplicantUserId = applicantUserId;
        CreatedAt = createdAt;
        Edit(applicantName, departmentName, legalEntity, documentNo, title, amount, repaymentDate, repaymentMethod, receiptReference, notes, otherInfo);
        Status = OaCashAdvanceRepaymentStatus.Draft;
    }

    public void Edit(string applicantName, string departmentName, string legalEntity, string documentNo, string title,
        decimal amount, DateOnly repaymentDate, OaCashAdvanceRepaymentMethod repaymentMethod, string receiptReference,
        string notes, string? otherInfo)
    {
        ApplicantName = Required(applicantName, "申请人");
        DepartmentName = Required(departmentName, "申请部门");
        LegalEntity = Required(legalEntity, "主体公司");
        DocumentNo = Required(documentNo, "还款单号");
        Title = Required(title, "还款标题");
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "还款金额必须大于 0。 ");
        if (repaymentDate == default) throw new ArgumentException("还款日期不能为空。", nameof(repaymentDate));
        ReceiptReference = Required(receiptReference, "收款凭据引用");
        Notes = Required(notes, "还款说明");
        Amount = decimal.Round(amount, 2);
        RepaymentDate = repaymentDate;
        RepaymentMethod = repaymentMethod;
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public void Submit(DateTime submittedAt)
    {
        if (Status is not (OaCashAdvanceRepaymentStatus.Draft or OaCashAdvanceRepaymentStatus.Rejected)) throw new InvalidOperationException("只有草稿或已驳回还款可以提交。 ");
        Status = OaCashAdvanceRepaymentStatus.Submitted;
        RejectionReason = null;
        SubmittedAt = submittedAt;
    }

    public void Approve()
    {
        if (Status != OaCashAdvanceRepaymentStatus.Submitted) throw new InvalidOperationException("只有已提交还款才能批准。 ");
        Status = OaCashAdvanceRepaymentStatus.Approved;
    }

    public void Reject(string? reason = null)
    {
        if (Status != OaCashAdvanceRepaymentStatus.Submitted) throw new InvalidOperationException("只有已提交还款才能驳回。 ");
        Status = OaCashAdvanceRepaymentStatus.Rejected;
        RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    public void Cancel()
    {
        if (Status is not (OaCashAdvanceRepaymentStatus.Draft or OaCashAdvanceRepaymentStatus.Submitted)) throw new InvalidOperationException("当前状态不能撤回还款。 ");
        Status = OaCashAdvanceRepaymentStatus.Cancelled;
    }

    /// <summary>仅供事务失败恢复或持久化重建使用。</summary>
    public void SetStatus(OaCashAdvanceRepaymentStatus status) => Status = status;

    private static string Required(string? value, string label) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{label}不能为空。") : value.Trim();
}
