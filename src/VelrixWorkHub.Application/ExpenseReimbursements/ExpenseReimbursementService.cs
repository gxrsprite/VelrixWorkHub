using VelrixWorkHub.Domain;
using VelrixWorkHub.Application.Workflow;

namespace VelrixWorkHub.Application.ExpenseReimbursements;

public interface IOaExpenseReimbursementRepository
{
    IReadOnlyList<OaExpenseReimbursement> List(Guid? applicantUserId = null);
    OaExpenseReimbursement? Get(Guid id);
    void Add(OaExpenseReimbursement item);
    void Update(OaExpenseReimbursement item);
}

public interface IOaExpenseLineRepository
{
    IReadOnlyList<OaExpenseLine> List(Guid? reimbursementId = null);
    OaExpenseLine? Get(Guid id);
    void Add(OaExpenseLine item);
    void Update(OaExpenseLine item);
    void Remove(Guid id);
}

public interface IOaExpenseReimbursementWorkflowApprover
{
    void ApplyApproval(OaExpenseReimbursement item);
    void ApplyRejection(OaExpenseReimbursement item, string? reason);
}

public sealed class ExpenseReimbursementService(
    IOaExpenseReimbursementRepository repository,
    IOaExpenseLineRepository lineRepository,
    WorkflowBindingService? bindings = null,
    IWorkflowTransactionBoundary? transactions = null) : IOaExpenseReimbursementWorkflowApprover
{
    public IReadOnlyList<OaExpenseReimbursement> ListMine(Guid applicantUserId)
        => applicantUserId == Guid.Empty ? [] : repository.List(applicantUserId).OrderByDescending(x => x.CreatedAt).ToArray();

    public IReadOnlyList<OaExpenseReimbursement> List() => repository.List().OrderByDescending(x => x.CreatedAt).ToArray();

    public OaExpenseReimbursement? Get(Guid id) => repository.Get(id);

    public OaExpenseReimbursement? GetByDocumentNo(string documentNo)
        => repository.List().SingleOrDefault(x => x.DocumentNo.Equals(documentNo.Trim(), StringComparison.OrdinalIgnoreCase));

    public void MarkReimbursedForPayment(OaExpenseReimbursement item)
    {
        if (item.Status == OaExpenseReimbursementStatus.Reimbursed) return;
        item.MarkReimbursed();
        repository.Update(item);
    }

    public void MarkPaidForPayment(OaExpenseReimbursement item)
    {
        if (item.Status == OaExpenseReimbursementStatus.Paid) return;
        item.MarkPaid();
        repository.Update(item);
    }

    public IReadOnlyList<OaExpenseLine> ListLines(Guid reimbursementId)
        => lineRepository.List(reimbursementId).OrderBy(x => x.BusinessDate).ThenBy(x => x.Id).ToArray();

    public OaExpenseReimbursement Create(Guid applicantUserId, string applicantName, string departmentName, string legalEntity,
        string documentNo, string title, DateOnly reimbursementDate, OaExpenseReimbursementType reimbursementType,
        Guid? projectId, bool isEntrusted, bool isTeamBuilding, bool isEntertainment, string reason, string? otherInfo)
    {
        EnsureDocumentNoUnique(documentNo, Guid.Empty);
        var item = new OaExpenseReimbursement(applicantUserId, applicantName, departmentName, legalEntity, documentNo, title,
            reimbursementDate, reimbursementType, projectId, isEntrusted, isTeamBuilding, isEntertainment, reason, otherInfo, DateTime.Now);
        repository.Add(item);
        return item;
    }

    public void Edit(OaExpenseReimbursement item, Guid actorUserId, string applicantName, string departmentName, string legalEntity,
        string documentNo, string title, DateOnly reimbursementDate, OaExpenseReimbursementType reimbursementType,
        Guid? projectId, bool isEntrusted, bool isTeamBuilding, bool isEntertainment, string reason, string? otherInfo)
    {
        EnsureOwner(item, actorUserId);
        EnsureEditable(item);
        EnsureDocumentNoUnique(documentNo, item.Id);
        item.Edit(applicantName, departmentName, legalEntity, documentNo, title, reimbursementDate, reimbursementType, projectId,
            isEntrusted, isTeamBuilding, isEntertainment, reason, otherInfo);
        repository.Update(item);
    }

    public OaExpenseLine AddLine(OaExpenseReimbursement item, Guid actorUserId, string expenseType, string description,
        string? invoiceNo, string? paymentFlowNo, DateOnly businessDate, decimal amount, decimal actualAmount, Guid? projectId, string? otherInfo)
    {
        EnsureOwner(item, actorUserId);
        EnsureEditable(item);
        var line = new OaExpenseLine(item.Id, expenseType, description, invoiceNo, paymentFlowNo, businessDate, amount, actualAmount, projectId, otherInfo);
        EnsureReferencesUnique(line, Guid.Empty);
        lineRepository.Add(line);
        Recalculate(item);
        return line;
    }

    public void EditLine(OaExpenseReimbursement item, Guid actorUserId, OaExpenseLine line, string expenseType, string description,
        string? invoiceNo, string? paymentFlowNo, DateOnly businessDate, decimal amount, decimal actualAmount, Guid? projectId, string? otherInfo)
    {
        EnsureOwner(item, actorUserId);
        EnsureEditable(item);
        if (line.ReimbursementId != item.Id || lineRepository.Get(line.Id) is null) throw new InvalidOperationException("费用明细不存在或不属于当前报销单。");
        line.Edit(expenseType, description, invoiceNo, paymentFlowNo, businessDate, amount, actualAmount, projectId, otherInfo);
        EnsureReferencesUnique(line, line.Id);
        lineRepository.Update(line);
        Recalculate(item);
    }

    public void RemoveLine(OaExpenseReimbursement item, Guid actorUserId, OaExpenseLine line)
    {
        EnsureOwner(item, actorUserId);
        EnsureEditable(item);
        if (line.ReimbursementId != item.Id) throw new InvalidOperationException("费用明细不属于当前报销单。");
        lineRepository.Remove(line.Id);
        Recalculate(item);
    }

    public void Submit(OaExpenseReimbursement item, Guid actorUserId)
    {
        EnsureOwner(item, actorUserId);
        EnsureSubmitReady(item);
        item.Submit(DateTime.Now);
        repository.Update(item);
    }

    public void SubmitAndStartWorkflow(OaExpenseReimbursement item, Guid actorUserId, string startedBy)
    {
        EnsureOwner(item, actorUserId);
        if (bindings is null) throw new InvalidOperationException("报销审批服务未配置。");
        EnsureSubmitReady(item);
        var previousStatus = item.Status;
        WorkflowInstance? workflow = null;
        void Core()
        {
            item.Submit(DateTime.Now);
            repository.Update(item);
            workflow = bindings.StartOrGet(WorkflowBindingCodes.ExpenseReimbursementApproval, nameof(OaExpenseReimbursement), item.Id, startedBy: startedBy);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => item.SetStatusForRecovery(previousStatus));
    }

    public void Cancel(OaExpenseReimbursement item, Guid actorUserId, string actor)
    {
        EnsureOwner(item, actorUserId);
        var running = bindings?.List(nameof(OaExpenseReimbursement), item.Id).SingleOrDefault(x => x.Status == WorkflowInstanceStatus.Running);
        var previousStatus = item.Status;
        void Core()
        {
            if (running is not null) bindings!.Withdraw(running.Id, actor, "申请人撤回报销单");
            item.Cancel();
            repository.Update(item);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => item.SetStatusForRecovery(previousStatus));
    }

    public void ApplyApproval(OaExpenseReimbursement item)
    {
        if (item.Status == OaExpenseReimbursementStatus.Approved) return;
        item.Approve();
        repository.Update(item);
    }

    public void ApplyRejection(OaExpenseReimbursement item, string? reason)
    {
        if (item.Status == OaExpenseReimbursementStatus.Rejected) return;
        item.Reject(reason);
        repository.Update(item);
    }

    private void EnsureSubmitReady(OaExpenseReimbursement item)
    {
        EnsureEditableOrRejected(item);
        var lines = ListLines(item.Id);
        if (lines.Count == 0) throw new InvalidOperationException("报销单至少需要一条费用明细。");
        EnsureAllReferencesUnique(lines);
        Recalculate(item);
    }

    private void Recalculate(OaExpenseReimbursement item)
    {
        item.SetActualAmount(lineRepository.List(item.Id).Sum(x => x.ActualAmount));
        repository.Update(item);
    }

    private void EnsureAllReferencesUnique(IEnumerable<OaExpenseLine> lines)
    {
        foreach (var line in lines) EnsureReferencesUnique(line, line.Id);
    }

    private void EnsureReferencesUnique(OaExpenseLine line, Guid ignoredLineId)
    {
        var invoice = NormalizeReference(line.InvoiceNo);
        var payment = NormalizeReference(line.PaymentFlowNo);
        if (invoice is null && payment is null) return;
        var activeReimbursementIds = repository.List()
            .Where(x => x.Status != OaExpenseReimbursementStatus.Cancelled)
            .Select(x => x.Id)
            .ToHashSet();
        var duplicate = lineRepository.List()
            .Where(x => x.Id != ignoredLineId && x.ReimbursementId != line.ReimbursementId && activeReimbursementIds.Contains(x.ReimbursementId))
            .FirstOrDefault(x => invoice is not null && string.Equals(invoice, NormalizeReference(x.InvoiceNo), StringComparison.OrdinalIgnoreCase)
                || payment is not null && string.Equals(payment, NormalizeReference(x.PaymentFlowNo), StringComparison.OrdinalIgnoreCase));
        if (duplicate is not null) throw new InvalidOperationException("发票号或付款流水号已被其他未取消报销单使用。");
        var sameDocumentDuplicate = lineRepository.List(line.ReimbursementId)
            .Where(x => x.Id != ignoredLineId)
            .FirstOrDefault(x => invoice is not null && string.Equals(invoice, NormalizeReference(x.InvoiceNo), StringComparison.OrdinalIgnoreCase)
                || payment is not null && string.Equals(payment, NormalizeReference(x.PaymentFlowNo), StringComparison.OrdinalIgnoreCase));
        if (sameDocumentDuplicate is not null) throw new InvalidOperationException("同一报销单内的发票号或付款流水号不能重复。");
    }

    private void EnsureDocumentNoUnique(string documentNo, Guid ignoredId)
    {
        if (repository.List().Any(x => x.Id != ignoredId && x.DocumentNo.Equals(documentNo.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("报销单号已存在。");
    }

    private static string? NormalizeReference(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void EnsureOwner(OaExpenseReimbursement item, Guid actorUserId) { if (actorUserId == Guid.Empty || item.ApplicantUserId != actorUserId) throw new UnauthorizedAccessException("当前用户不能操作其他员工的报销单。"); }
    private static void EnsureEditable(OaExpenseReimbursement item) { if (item.Status is not (OaExpenseReimbursementStatus.Draft or OaExpenseReimbursementStatus.Rejected)) throw new InvalidOperationException("只有草稿或已驳回报销单可以编辑费用内容。"); }
    private static void EnsureEditableOrRejected(OaExpenseReimbursement item) { if (item.Status is not (OaExpenseReimbursementStatus.Draft or OaExpenseReimbursementStatus.Rejected)) throw new InvalidOperationException("当前状态不能提交报销单。"); }
}

internal static class OaExpenseReimbursementRecoveryExtensions
{
    public static void SetStatusForRecovery(this OaExpenseReimbursement item, OaExpenseReimbursementStatus status)
        => item.SetStatus(status);
}
