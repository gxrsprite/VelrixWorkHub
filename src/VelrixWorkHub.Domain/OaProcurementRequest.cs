namespace VelrixWorkHub.Domain;

public enum OaProcurementRequestType
{
    ProductRelated,
    NonProductRelated,
    OfficeSupply,
    Sourcing
}

public enum OaProcurementRequestStatus
{
    Draft,
    Submitted,
    Rejected,
    Approved,
    Cancelled
}

/// <summary>OA 采购意图申请，不代表 ERP 采购订单或库存占用。</summary>
public sealed class OaProcurementRequest
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string DocumentNo { get; private set; } = string.Empty;
    public Guid ApplicantUserId { get; init; }
    public string ApplicantName { get; private set; } = string.Empty;
    public string DepartmentName { get; private set; } = string.Empty;
    public string LegalEntity { get; private set; } = string.Empty;
    public OaProcurementRequestType RequestType { get; private set; }
    public DateOnly RequestDate { get; private set; }
    public DateOnly RequiredDate { get; private set; }
    public Guid? ProjectId { get; private set; }
    public string? BudgetReference { get; private set; }
    public decimal EstimatedAmount { get; private set; }
    public string Purpose { get; private set; } = string.Empty;
    public string OtherInfo { get; private set; } = "{}";
    public OaProcurementRequestStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? SubmittedAt { get; private set; }

    public OaProcurementRequest(Guid applicantUserId, string applicantName, string departmentName, string legalEntity,
        string documentNo, OaProcurementRequestType requestType, DateOnly requestDate, DateOnly requiredDate,
        Guid? projectId, string? budgetReference, string purpose, string? otherInfo, DateTime createdAt)
    {
        if (applicantUserId == Guid.Empty) throw new ArgumentException("申请人不能为空。", nameof(applicantUserId));
        ApplicantUserId = applicantUserId;
        CreatedAt = createdAt;
        Edit(applicantName, departmentName, legalEntity, documentNo, requestType, requestDate, requiredDate, projectId, budgetReference, purpose, otherInfo);
        Status = OaProcurementRequestStatus.Draft;
    }

    public void Edit(string applicantName, string departmentName, string legalEntity, string documentNo,
        OaProcurementRequestType requestType, DateOnly requestDate, DateOnly requiredDate, Guid? projectId,
        string? budgetReference, string purpose, string? otherInfo)
    {
        DocumentNo = Required(documentNo, "采购申请单号");
        ApplicantName = Required(applicantName, "申请人");
        DepartmentName = Required(departmentName, "申请部门");
        LegalEntity = Required(legalEntity, "主体公司");
        if (requestDate == default) throw new ArgumentException("申请日期不能为空。", nameof(requestDate));
        if (requiredDate < requestDate) throw new ArgumentException("需求日期不能早于申请日期。", nameof(requiredDate));
        if (string.IsNullOrWhiteSpace(purpose)) throw new ArgumentException("采购事由不能为空。", nameof(purpose));
        RequestType = requestType;
        RequestDate = requestDate;
        RequiredDate = requiredDate;
        ProjectId = projectId;
        BudgetReference = Clean(budgetReference);
        Purpose = purpose.Trim();
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public void Submit(DateTime submittedAt)
    {
        if (Status is not (OaProcurementRequestStatus.Draft or OaProcurementRequestStatus.Rejected)) throw new InvalidOperationException("只有草稿或已驳回采购申请才能提交。");
        Status = OaProcurementRequestStatus.Submitted;
        RejectionReason = null;
        SubmittedAt = submittedAt;
    }

    public void Approve()
    {
        if (Status != OaProcurementRequestStatus.Submitted) throw new InvalidOperationException("只有已提交采购申请才能批准。");
        Status = OaProcurementRequestStatus.Approved;
    }

    public void Reject(string? reason = null)
    {
        if (Status != OaProcurementRequestStatus.Submitted) throw new InvalidOperationException("只有已提交采购申请才能驳回。");
        Status = OaProcurementRequestStatus.Rejected;
        RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    public void Cancel()
    {
        if (Status is not (OaProcurementRequestStatus.Draft or OaProcurementRequestStatus.Submitted)) throw new InvalidOperationException("当前状态不能撤回采购申请。");
        Status = OaProcurementRequestStatus.Cancelled;
    }

    /// <summary>仅供明细变更后的汇总和持久化恢复使用。</summary>
    public void SetEstimatedAmount(decimal amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        EstimatedAmount = decimal.Round(amount, 2);
    }

    /// <summary>仅供事务失败恢复或持久化重建使用。</summary>
    public void SetStatus(OaProcurementRequestStatus status) => Status = status;

    private static string Required(string? value, string label) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{label}不能为空。") : value.Trim();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class OaProcurementRequestLine
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid RequestId { get; init; }
    public Guid? ProductId { get; init; }
    public string ItemName { get; private set; } = string.Empty;
    public string MaterialCategory { get; private set; } = string.Empty;
    public string Specification { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public string Unit { get; private set; } = string.Empty;
    public decimal EstimatedUnitPrice { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public OaProcurementRequestLine(Guid requestId, Guid? productId, string itemName, string materialCategory,
        string specification, decimal quantity, string unit, decimal estimatedUnitPrice, string? otherInfo)
    {
        if (requestId == Guid.Empty) throw new ArgumentException("采购申请不能为空。", nameof(requestId));
        if (productId == Guid.Empty) productId = null;
        RequestId = requestId;
        ProductId = productId;
        ItemName = Required(itemName, "采购物品");
        MaterialCategory = Required(materialCategory, "物料分类");
        Specification = Required(specification, "规格/技术要求");
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "采购数量必须大于 0。");
        Unit = Required(unit, "计量单位");
        if (estimatedUnitPrice < 0) throw new ArgumentOutOfRangeException(nameof(estimatedUnitPrice), "预计单价不能为负数。");
        Quantity = quantity;
        EstimatedUnitPrice = decimal.Round(estimatedUnitPrice, 2);
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public decimal EstimatedAmount => decimal.Round(Quantity * EstimatedUnitPrice, 2);
    private static string Required(string? value, string label) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{label}不能为空。") : value.Trim();
}
