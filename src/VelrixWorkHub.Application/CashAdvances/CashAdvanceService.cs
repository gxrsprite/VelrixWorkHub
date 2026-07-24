using VelrixWorkHub.Application.ExpenseReimbursements;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.CashAdvances;

public interface IOaCashAdvanceRepository
{
    IReadOnlyList<OaCashAdvance> List(Guid? applicantUserId = null);
    OaCashAdvance? Get(Guid id);
    void Add(OaCashAdvance item);
    void Update(OaCashAdvance item);
}

public interface IOaCashAdvanceOffsetRepository
{
    IReadOnlyList<OaCashAdvanceOffset> List(Guid? cashAdvanceId = null);
    void Add(OaCashAdvanceOffset item);
}

public interface IOaCashAdvanceWorkflowApprover
{
    void ApplyApproval(OaCashAdvance item);
    void ApplyRejection(OaCashAdvance item, string? reason);
}

public sealed class CashAdvanceService(
    IOaCashAdvanceRepository repository,
    IOaCashAdvanceOffsetRepository offsetRepository,
    ExpenseReimbursementService reimbursementService,
    WorkflowBindingService? bindings = null,
    IWorkflowTransactionBoundary? transactions = null) : IOaCashAdvanceWorkflowApprover
{
    public IReadOnlyList<OaCashAdvance> ListMine(Guid applicantUserId)
        => applicantUserId == Guid.Empty ? [] : repository.List(applicantUserId).OrderByDescending(x => x.CreatedAt).ToArray();

    public IReadOnlyList<OaCashAdvance> List() => repository.List().OrderByDescending(x => x.CreatedAt).ToArray();

    public OaCashAdvance? Get(Guid id) => repository.Get(id);

    public IReadOnlyList<OaCashAdvanceOffset> ListOffsets(Guid cashAdvanceId)
        => offsetRepository.List(cashAdvanceId).OrderByDescending(x => x.OffsetDate).ToArray();

    public OaCashAdvance Create(Guid applicantUserId, string applicantName, string departmentName, string legalEntity,
        string documentNo, string title, OaCashAdvanceType advanceType, DateOnly requestDate, DateOnly expectedSettlementDate,
        Guid? projectId, decimal amount, string purpose, string? otherInfo)
    {
        EnsureDocumentNoUnique(documentNo, Guid.Empty);
        var item = new OaCashAdvance(applicantUserId, applicantName, departmentName, legalEntity, documentNo, title, advanceType,
            requestDate, expectedSettlementDate, projectId, amount, purpose, otherInfo, DateTime.Now);
        repository.Add(item);
        return item;
    }

    public void Edit(OaCashAdvance item, Guid actorUserId, string applicantName, string departmentName, string legalEntity,
        string documentNo, string title, OaCashAdvanceType advanceType, DateOnly requestDate, DateOnly expectedSettlementDate,
        Guid? projectId, decimal amount, string purpose, string? otherInfo)
    {
        EnsureOwner(item, actorUserId);
        EnsureEditable(item);
        EnsureDocumentNoUnique(documentNo, item.Id);
        item.Edit(applicantName, departmentName, legalEntity, documentNo, title, advanceType, requestDate, expectedSettlementDate,
            projectId, amount, purpose, otherInfo);
        repository.Update(item);
    }

    public void Submit(OaCashAdvance item, Guid actorUserId)
    {
        EnsureOwner(item, actorUserId);
        EnsureSubmitReady(item);
        item.Submit(DateTime.Now);
        repository.Update(item);
    }

    public void SubmitAndStartWorkflow(OaCashAdvance item, Guid actorUserId, string startedBy)
    {
        EnsureOwner(item, actorUserId);
        if (bindings is null) throw new InvalidOperationException("借款审批服务未配置。");
        EnsureSubmitReady(item);
        var previousStatus = item.Status;
        void Core()
        {
            item.Submit(DateTime.Now);
            repository.Update(item);
            bindings.StartOrGet(WorkflowBindingCodes.CashAdvanceApproval, nameof(OaCashAdvance), item.Id, startedBy: startedBy);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => item.SetStatusForRecovery(previousStatus));
    }

    public void Cancel(OaCashAdvance item, Guid actorUserId, string actor)
    {
        EnsureOwner(item, actorUserId);
        var running = bindings?.List(nameof(OaCashAdvance), item.Id).SingleOrDefault(x => x.Status == WorkflowInstanceStatus.Running);
        var previousStatus = item.Status;
        void Core()
        {
            if (running is not null) bindings!.Withdraw(running.Id, actor, "申请人撤回借款");
            item.Cancel();
            repository.Update(item);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => item.SetStatusForRecovery(previousStatus));
    }

    public OaCashAdvanceOffset ApplyOffset(OaCashAdvance item, Guid actorUserId, Guid reimbursementId, decimal amount,
        DateOnly offsetDate, string notes, string? otherInfo)
    {
        EnsureOwner(item, actorUserId);
        if (item.Status is not (OaCashAdvanceStatus.Approved or OaCashAdvanceStatus.PartiallySettled)) throw new InvalidOperationException("只有已批准或部分冲销的借款才能冲销。");
        var reimbursement = reimbursementService.Get(reimbursementId) ?? throw new InvalidOperationException("关联的报销单不存在。");
        if (reimbursement.ApplicantUserId != item.ApplicantUserId) throw new UnauthorizedAccessException("借款只能冲销同一申请人的报销单。");
        if (reimbursement.Status is not (OaExpenseReimbursementStatus.Approved or OaExpenseReimbursementStatus.Reimbursed)) throw new InvalidOperationException("只有已批准或已报销的报销单可以冲销借款。");
        if (amount > reimbursement.ActualAmount) throw new InvalidOperationException("冲销金额不能超过报销单实报金额。");
        if (offsetRepository.List().Any(x => x.ReimbursementId == reimbursementId)) throw new InvalidOperationException("该报销单已经冲销过借款，不能重复关联。");
        var offset = new OaCashAdvanceOffset(item.Id, reimbursementId, amount, offsetDate, notes, otherInfo);
        var previousAmount = item.SettledAmount;
        var previousStatus = item.Status;
        void Core()
        {
            item.ApplyOffset(amount);
            offsetRepository.Add(offset);
            repository.Update(item);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => { item.SetSettledAmount(previousAmount); item.SetStatus(previousStatus); });
        return offset;
    }

    public void ApplyApprovedRepaymentSettlement(OaCashAdvance item, Guid applicantUserId, decimal amount)
    {
        EnsureOwner(item, applicantUserId);
        if (item.Status is not (OaCashAdvanceStatus.Approved or OaCashAdvanceStatus.PartiallySettled)) throw new InvalidOperationException("只有已批准或部分结清的借款可以登记还款。 ");
        item.ApplySettlement(amount);
        repository.Update(item);
    }

    public void ApplyApproval(OaCashAdvance item)
    {
        if (item.Status == OaCashAdvanceStatus.Approved) return;
        item.Approve();
        repository.Update(item);
    }

    public void ApplyRejection(OaCashAdvance item, string? reason)
    {
        if (item.Status == OaCashAdvanceStatus.Rejected) return;
        item.Reject(reason);
        repository.Update(item);
    }

    private void EnsureSubmitReady(OaCashAdvance item)
    {
        EnsureEditableOrRejected(item);
        if (item.Amount <= 0) throw new InvalidOperationException("借款金额必须大于 0。");
    }

    private void EnsureDocumentNoUnique(string documentNo, Guid ignoredId)
    {
        if (repository.List().Any(x => x.Id != ignoredId && x.DocumentNo.Equals(documentNo.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("借款单号已存在。");
    }

    private static void EnsureOwner(OaCashAdvance item, Guid actorUserId) { if (actorUserId == Guid.Empty || item.ApplicantUserId != actorUserId) throw new UnauthorizedAccessException("当前用户不能操作其他员工的借款。"); }
    private static void EnsureEditable(OaCashAdvance item) { if (item.Status is not (OaCashAdvanceStatus.Draft or OaCashAdvanceStatus.Rejected)) throw new InvalidOperationException("只有草稿或已驳回借款可以编辑。"); }
    private static void EnsureEditableOrRejected(OaCashAdvance item) { if (item.Status is not (OaCashAdvanceStatus.Draft or OaCashAdvanceStatus.Rejected)) throw new InvalidOperationException("当前状态不能提交借款。"); }
}

internal static class OaCashAdvanceRecoveryExtensions
{
    public static void SetStatusForRecovery(this OaCashAdvance item, OaCashAdvanceStatus status) => item.SetStatus(status);
}
