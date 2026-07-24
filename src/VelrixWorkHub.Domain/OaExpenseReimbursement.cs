namespace VelrixWorkHub.Domain;

public enum OaExpenseReimbursementType
{
    General,
    Travel,
    Entertainment,
    TeamBuilding,
    Other
}

public enum OaExpenseReimbursementStatus
{
    Draft,
    Submitted,
    Rejected,
    Approved,
    Reimbursed,
    Paid,
    Cancelled
}

/// <summary>OA 报销主单。它只记录申请与审批状态，不代表 ERP 付款或核销流水。</summary>
public sealed class OaExpenseReimbursement
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string DocumentNo { get; private set; } = string.Empty;
    public Guid ApplicantUserId { get; init; }
    public string ApplicantName { get; private set; } = string.Empty;
    public string DepartmentName { get; private set; } = string.Empty;
    public string LegalEntity { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public DateOnly ReimbursementDate { get; private set; }
    public OaExpenseReimbursementType ReimbursementType { get; private set; }
    public Guid? ProjectId { get; private set; }
    public bool IsEntrusted { get; private set; }
    public bool IsTeamBuilding { get; private set; }
    public bool IsEntertainment { get; private set; }
    public decimal ActualAmount { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string OtherInfo { get; private set; } = "{}";
    public OaExpenseReimbursementStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? SubmittedAt { get; private set; }

    public OaExpenseReimbursement(Guid applicantUserId, string applicantName, string departmentName, string legalEntity,
        string documentNo, string title, DateOnly reimbursementDate, OaExpenseReimbursementType reimbursementType,
        Guid? projectId, bool isEntrusted, bool isTeamBuilding, bool isEntertainment, string reason, string? otherInfo, DateTime createdAt)
    {
        if (applicantUserId == Guid.Empty) throw new ArgumentException("申请人不能为空。", nameof(applicantUserId));
        ApplicantUserId = applicantUserId;
        CreatedAt = createdAt;
        Edit(applicantName, departmentName, legalEntity, documentNo, title, reimbursementDate, reimbursementType, projectId, isEntrusted, isTeamBuilding, isEntertainment, reason, otherInfo);
        Status = OaExpenseReimbursementStatus.Draft;
    }

    public void Edit(string applicantName, string departmentName, string legalEntity, string documentNo, string title,
        DateOnly reimbursementDate, OaExpenseReimbursementType reimbursementType, Guid? projectId, bool isEntrusted,
        bool isTeamBuilding, bool isEntertainment, string reason, string? otherInfo)
    {
        DocumentNo = Required(documentNo, "报销单号");
        ApplicantName = Required(applicantName, "申请人");
        DepartmentName = Required(departmentName, "申请部门");
        LegalEntity = Required(legalEntity, "主体公司");
        Title = Required(title, "报销标题");
        if (reimbursementDate == default) throw new ArgumentException("报销日期不能为空。", nameof(reimbursementDate));
        if (isTeamBuilding && isEntertainment) throw new ArgumentException("团建和业务招待不能同时勾选。", nameof(isEntertainment));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("报销事由不能为空。", nameof(reason));
        ReimbursementDate = reimbursementDate;
        ReimbursementType = reimbursementType;
        ProjectId = projectId;
        IsEntrusted = isEntrusted;
        IsTeamBuilding = isTeamBuilding;
        IsEntertainment = isEntertainment;
        Reason = reason.Trim();
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public void SetActualAmount(decimal amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "报销金额不能为负数。");
        ActualAmount = decimal.Round(amount, 2);
    }

    public void Submit(DateTime submittedAt)
    {
        if (Status is not (OaExpenseReimbursementStatus.Draft or OaExpenseReimbursementStatus.Rejected))
            throw new InvalidOperationException("只有草稿或已驳回的报销单才能提交。");
        if (ActualAmount <= 0) throw new InvalidOperationException("报销单至少需要一条有效费用明细。");
        Status = OaExpenseReimbursementStatus.Submitted;
        RejectionReason = null;
        SubmittedAt = submittedAt;
    }

    public void Approve()
    {
        if (Status != OaExpenseReimbursementStatus.Submitted) throw new InvalidOperationException("只有已提交的报销单才能批准。");
        Status = OaExpenseReimbursementStatus.Approved;
    }

    public void Reject(string? reason = null)
    {
        if (Status != OaExpenseReimbursementStatus.Submitted) throw new InvalidOperationException("只有已提交的报销单才能驳回。");
        Status = OaExpenseReimbursementStatus.Rejected;
        RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    public void Cancel()
    {
        if (Status is not (OaExpenseReimbursementStatus.Draft or OaExpenseReimbursementStatus.Submitted))
            throw new InvalidOperationException("当前状态不能撤回报销单。");
        Status = OaExpenseReimbursementStatus.Cancelled;
    }

    public void MarkReimbursed()
    {
        if (Status != OaExpenseReimbursementStatus.Approved) throw new InvalidOperationException("只有已批准的报销单才能标记为已报销。");
        Status = OaExpenseReimbursementStatus.Reimbursed;
    }

    public void MarkPaid()
    {
        if (Status != OaExpenseReimbursementStatus.Reimbursed) throw new InvalidOperationException("只有已报销的报销单才能标记为已付款。");
        Status = OaExpenseReimbursementStatus.Paid;
    }

    /// <summary>仅供持久化事务失败恢复或动作处理器使用；普通状态变更必须走领域方法。</summary>
    public void SetStatus(OaExpenseReimbursementStatus status) => Status = status;

    private static string Required(string? value, string label) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{label}不能为空。") : value.Trim();
}

public sealed class OaExpenseLine
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ReimbursementId { get; init; }
    public string ExpenseType { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string? InvoiceNo { get; private set; }
    public string? PaymentFlowNo { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public decimal Amount { get; private set; }
    public decimal ActualAmount { get; private set; }
    public Guid? ProjectId { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public OaExpenseLine(Guid reimbursementId, string expenseType, string description, string? invoiceNo, string? paymentFlowNo,
        DateOnly businessDate, decimal amount, decimal actualAmount, Guid? projectId, string? otherInfo)
    {
        if (reimbursementId == Guid.Empty) throw new ArgumentException("报销主单不能为空。", nameof(reimbursementId));
        ReimbursementId = reimbursementId;
        Edit(expenseType, description, invoiceNo, paymentFlowNo, businessDate, amount, actualAmount, projectId, otherInfo);
    }

    public void Edit(string expenseType, string description, string? invoiceNo, string? paymentFlowNo, DateOnly businessDate,
        decimal amount, decimal actualAmount, Guid? projectId, string? otherInfo)
    {
        if (string.IsNullOrWhiteSpace(expenseType)) throw new ArgumentException("费用类型不能为空。", nameof(expenseType));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("费用说明不能为空。", nameof(description));
        if (businessDate == default) throw new ArgumentException("费用发生日期不能为空。", nameof(businessDate));
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "费用金额必须大于 0。");
        if (actualAmount <= 0 || actualAmount > amount) throw new ArgumentOutOfRangeException(nameof(actualAmount), "实报金额必须大于 0 且不能超过费用金额。");
        ExpenseType = expenseType.Trim();
        Description = description.Trim();
        InvoiceNo = Clean(invoiceNo);
        PaymentFlowNo = Clean(paymentFlowNo);
        BusinessDate = businessDate;
        Amount = decimal.Round(amount, 2);
        ActualAmount = decimal.Round(actualAmount, 2);
        ProjectId = projectId;
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
