using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PaymentRequests;

public interface IOaPaymentRequestRepository
{
    IReadOnlyList<OaPaymentRequest> List(Guid? applicantUserId = null);
    OaPaymentRequest? Get(Guid id);
    void Add(OaPaymentRequest item);
    void Update(OaPaymentRequest item);
}

public interface IOaPaymentRequestWorkflowApprover
{
    void ApplyApproval(OaPaymentRequest item, string? actorName = null);
    void ApplyRejection(OaPaymentRequest item, string? reason, string? actorName = null);
}

public interface IOaPaymentRequestStatusHistoryRepository
{
    IReadOnlyList<OaPaymentRequestStatusHistory> List(Guid paymentRequestId);
    void Add(OaPaymentRequestStatusHistory item);
}

public sealed class PaymentRequestService(
    IOaPaymentRequestRepository repository,
    WorkflowBindingService? bindings = null,
    IWorkflowTransactionBoundary? transactions = null,
    IOaPaymentRequestStatusHistoryRepository? statusHistory = null,
    PaymentBudgetService? budgets = null) : IOaPaymentRequestWorkflowApprover
{
    public IReadOnlyList<OaPaymentRequest> ListMine(Guid applicantUserId)
        => applicantUserId == Guid.Empty ? [] : repository.List(applicantUserId).OrderByDescending(x => x.CreatedAt).ToArray();

    public IReadOnlyList<OaPaymentRequest> List() => repository.List().OrderByDescending(x => x.CreatedAt).ToArray();
    public OaPaymentRequest? Get(Guid id) => repository.Get(id);

    public OaPaymentRequest Create(Guid applicantUserId, string applicantName, string departmentName, string legalEntity,
        string documentNo, string payeeName, string payeeAccountReference, string paymentBankName, string currency, decimal amount,
        OaPaymentRequestType paymentType, DateOnly requestDate, DateOnly requestedPaymentDate, string? precedingDocumentNo,
        Guid? projectId, string purpose, string? otherInfo, string? budgetReference = null)
    {
        EnsureDocumentNoUnique(documentNo, Guid.Empty);
        var item = new OaPaymentRequest(applicantUserId, applicantName, departmentName, legalEntity, documentNo, payeeName,
            payeeAccountReference, paymentBankName, currency, amount, paymentType, requestDate, requestedPaymentDate,
            precedingDocumentNo, projectId, purpose, otherInfo, DateTime.Now, budgetReference);
        repository.Add(item);
        return item;
    }

    public void Edit(OaPaymentRequest item, Guid actorUserId, string applicantName, string departmentName, string legalEntity,
        string documentNo, string payeeName, string payeeAccountReference, string paymentBankName, string currency, decimal amount,
        OaPaymentRequestType paymentType, DateOnly requestDate, DateOnly requestedPaymentDate, string? precedingDocumentNo,
        Guid? projectId, string purpose, string? otherInfo, string? budgetReference = null)
    {
        EnsureOwner(item, actorUserId);
        EnsureEditable(item);
        EnsureDocumentNoUnique(documentNo, item.Id);
        item.Edit(applicantName, departmentName, legalEntity, documentNo, payeeName, payeeAccountReference, paymentBankName, currency,
            amount, paymentType, requestDate, requestedPaymentDate, precedingDocumentNo, projectId, purpose, otherInfo);
        item.SetBudgetReference(budgetReference);
        repository.Update(item);
    }

    public void Submit(OaPaymentRequest item, Guid actorUserId)
    {
        EnsureOwner(item, actorUserId);
        EnsureSubmitReady(item);
        var previousStatus = item.Status;
        void Core()
        {
            budgets?.ReserveForSubmission(item);
            item.Submit(DateTime.Now);
            repository.Update(item);
            AddHistory(item, previousStatus, actorUserId.ToString(), "提交付款申请");
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => item.SetStatusForRecovery(previousStatus));
    }

    public void SubmitAndStartWorkflow(OaPaymentRequest item, Guid actorUserId, string startedBy)
    {
        EnsureOwner(item, actorUserId);
        if (bindings is null) throw new InvalidOperationException("付款申请审批服务未配置。");
        EnsureSubmitReady(item);
        var previousStatus = item.Status;
        void Core()
        {
            budgets?.ReserveForSubmission(item);
            item.Submit(DateTime.Now);
            repository.Update(item);
            AddHistory(item, previousStatus, startedBy, "提交付款申请");
            bindings.StartOrGet(WorkflowBindingCodes.PaymentRequestApproval, nameof(OaPaymentRequest), item.Id, startedBy: startedBy);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => item.SetStatusForRecovery(previousStatus));
    }

    public void Cancel(OaPaymentRequest item, Guid actorUserId, string actor)
    {
        EnsureOwner(item, actorUserId);
        var running = bindings?.List(nameof(OaPaymentRequest), item.Id).SingleOrDefault(x => x.Status == WorkflowInstanceStatus.Running);
        var previousStatus = item.Status;
        void Core()
        {
            if (running is not null) bindings!.Withdraw(running.Id, actor, "申请人撤回付款申请");
            item.Cancel();
            budgets?.ReleaseForRequest(item);
            repository.Update(item);
            AddHistory(item, previousStatus, actor, "撤回付款申请");
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => item.SetStatusForRecovery(previousStatus));
    }

    public void ApplyApproval(OaPaymentRequest item, string? actorName = null)
    {
        if (item.Status == OaPaymentRequestStatus.Approved) return;
        var previousStatus = item.Status;
        item.Approve();
        repository.Update(item);
        AddHistory(item, previousStatus, actorName ?? "Workflow", "Workflow 批准付款申请");
    }

    public void ApplyRejection(OaPaymentRequest item, string? reason, string? actorName = null)
    {
        if (item.Status == OaPaymentRequestStatus.Rejected) return;
        var previousStatus = item.Status;
        item.Reject(reason);
        budgets?.ReleaseForRequest(item);
        repository.Update(item);
        AddHistory(item, previousStatus, actorName ?? "Workflow", reason ?? "Workflow 驳回付款申请");
    }

    public IReadOnlyList<OaPaymentRequest> ListPendingFinanceReview()
        => repository.List().Where(x => x.Status == OaPaymentRequestStatus.Approved && x.FinanceReviewStatus == OaPaymentFinanceReviewStatus.Pending)
            .OrderBy(x => x.RequestedPaymentDate).ThenBy(x => x.CreatedAt).ToArray();

    public void ReviewFinance(OaPaymentRequest item, string reviewer, bool approved, string? reason, bool canReview)
    {
        if (!canReview) throw new UnauthorizedAccessException("当前用户没有财务复核付款申请的权限。");
        var previousStatus = item.Status;
        var previousFinanceStatus = item.FinanceReviewStatus;
        var previousFinanceReason = item.FinanceReviewReason;
        var previousReviewer = item.FinanceReviewer;
        var previousReviewedAt = item.FinanceReviewedAt;
        var previousRejectionReason = item.RejectionReason;
        void Core()
        {
            item.ReviewFinance(reviewer, approved, reason);
            if (!approved)
            {
                item.SetStatus(OaPaymentRequestStatus.Rejected);
                budgets?.ReleaseForRequest(item);
            }
            repository.Update(item);
            if (!approved) AddHistory(item, previousStatus, reviewer, reason ?? "财务复核不通过");
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ =>
        {
            item.SetStatus(previousStatus);
            item.RestoreFinanceReviewForRecovery(previousFinanceStatus, previousFinanceReason, previousReviewer, previousReviewedAt, previousRejectionReason);
        });
    }

    public void MarkPaid(OaPaymentRequest item, string? actorName = null)
    {
        var previousStatus = item.Status;
        budgets?.ConsumeForPayment(item);
        item.MarkPaid();
        repository.Update(item);
        AddHistory(item, previousStatus, actorName ?? "Finance", "登记实际付款");
    }

    public IReadOnlyList<OaPaymentRequestStatusHistory> ListHistory(Guid paymentRequestId)
        => statusHistory?.List(paymentRequestId).OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id).ToArray() ?? [];

    private void AddHistory(OaPaymentRequest item, OaPaymentRequestStatus fromStatus, string actorName, string? reason)
        => statusHistory?.Add(new OaPaymentRequestStatusHistory(item.Id, fromStatus, item.Status, reason, actorName, DateTime.Now));

    private void EnsureSubmitReady(OaPaymentRequest item)
    {
        EnsureEditableOrRejected(item);
        if (item.Amount <= 0) throw new InvalidOperationException("付款金额必须大于 0。");
        if (string.IsNullOrWhiteSpace(item.PrecedingDocumentNo)) throw new InvalidOperationException("付款申请必须填写前置单据号或业务依据。");
    }

    private void EnsureDocumentNoUnique(string documentNo, Guid ignoredId)
    {
        if (repository.List().Any(x => x.Id != ignoredId && x.DocumentNo.Equals(documentNo.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("付款申请单号已存在。");
    }

    private static void EnsureOwner(OaPaymentRequest item, Guid actorUserId) { if (actorUserId == Guid.Empty || item.ApplicantUserId != actorUserId) throw new UnauthorizedAccessException("当前用户不能操作其他员工的付款申请。"); }
    private static void EnsureEditable(OaPaymentRequest item) { if (item.Status is not (OaPaymentRequestStatus.Draft or OaPaymentRequestStatus.Rejected)) throw new InvalidOperationException("只有草稿或已驳回付款申请可以编辑。"); }
    private static void EnsureEditableOrRejected(OaPaymentRequest item) { if (item.Status is not (OaPaymentRequestStatus.Draft or OaPaymentRequestStatus.Rejected)) throw new InvalidOperationException("当前状态不能提交付款申请。"); }
}

internal static class OaPaymentRequestRecoveryExtensions
{
    public static void SetStatusForRecovery(this OaPaymentRequest item, OaPaymentRequestStatus status) => item.SetStatus(status);
}
