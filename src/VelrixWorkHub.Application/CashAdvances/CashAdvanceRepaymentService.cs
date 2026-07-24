using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.CashAdvances;

public interface IOaCashAdvanceRepaymentRepository
{
    IReadOnlyList<OaCashAdvanceRepayment> List(Guid? applicantUserId = null, Guid? cashAdvanceId = null);
    OaCashAdvanceRepayment? Get(Guid id);
    void Add(OaCashAdvanceRepayment item);
    void Update(OaCashAdvanceRepayment item);
}

public interface IOaCashAdvanceRepaymentWorkflowApprover
{
    void ApplyApproval(OaCashAdvanceRepayment item);
    void ApplyRejection(OaCashAdvanceRepayment item, string? reason);
}

public sealed class CashAdvanceRepaymentService(
    IOaCashAdvanceRepaymentRepository repository,
    CashAdvanceService cashAdvanceService,
    WorkflowBindingService? bindings = null,
    IWorkflowTransactionBoundary? transactions = null) : IOaCashAdvanceRepaymentWorkflowApprover
{
    public IReadOnlyList<OaCashAdvanceRepayment> ListMine(Guid applicantUserId)
        => applicantUserId == Guid.Empty ? [] : repository.List(applicantUserId: applicantUserId).OrderByDescending(x => x.CreatedAt).ToArray();

    public IReadOnlyList<OaCashAdvanceRepayment> ListByCashAdvance(Guid cashAdvanceId)
        => cashAdvanceId == Guid.Empty ? [] : repository.List(cashAdvanceId: cashAdvanceId).OrderByDescending(x => x.CreatedAt).ToArray();

    public OaCashAdvanceRepayment? Get(Guid id) => repository.Get(id);

    public OaCashAdvanceRepayment Create(Guid cashAdvanceId, Guid applicantUserId, string applicantName, string departmentName,
        string legalEntity, string documentNo, string title, decimal amount, DateOnly repaymentDate,
        OaCashAdvanceRepaymentMethod repaymentMethod, string receiptReference, string notes, string? otherInfo)
    {
        EnsureDocumentNoUnique(documentNo, Guid.Empty);
        EnsureAdvanceAvailable(cashAdvanceId, applicantUserId, amount);
        var item = new OaCashAdvanceRepayment(cashAdvanceId, applicantUserId, applicantName, departmentName, legalEntity,
            documentNo, title, amount, repaymentDate, repaymentMethod, receiptReference, notes, otherInfo, DateTime.Now);
        repository.Add(item);
        return item;
    }

    public void Edit(OaCashAdvanceRepayment item, Guid actorUserId, string applicantName, string departmentName, string legalEntity,
        string documentNo, string title, decimal amount, DateOnly repaymentDate, OaCashAdvanceRepaymentMethod repaymentMethod,
        string receiptReference, string notes, string? otherInfo)
    {
        EnsureOwner(item, actorUserId);
        EnsureEditable(item);
        EnsureDocumentNoUnique(documentNo, item.Id);
        EnsureAdvanceAvailable(item.CashAdvanceId, actorUserId, amount);
        item.Edit(applicantName, departmentName, legalEntity, documentNo, title, amount, repaymentDate, repaymentMethod, receiptReference, notes, otherInfo);
        repository.Update(item);
    }

    public void SubmitAndStartWorkflow(OaCashAdvanceRepayment item, Guid actorUserId, string startedBy)
    {
        EnsureOwner(item, actorUserId);
        if (bindings is null) throw new InvalidOperationException("还款审批服务未配置。 ");
        EnsureSubmitReady(item);
        EnsureAdvanceAvailable(item.CashAdvanceId, actorUserId, item.Amount);
        var previousStatus = item.Status;
        void Core()
        {
            item.Submit(DateTime.Now);
            repository.Update(item);
            bindings.StartOrGet(WorkflowBindingCodes.CashAdvanceRepaymentApproval, nameof(OaCashAdvanceRepayment), item.Id, startedBy: startedBy);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => item.SetStatus(previousStatus));
    }

    public void Cancel(OaCashAdvanceRepayment item, Guid actorUserId, string actor)
    {
        EnsureOwner(item, actorUserId);
        var running = bindings?.List(nameof(OaCashAdvanceRepayment), item.Id).SingleOrDefault(x => x.Status == WorkflowInstanceStatus.Running);
        var previousStatus = item.Status;
        void Core()
        {
            if (running is not null) bindings!.Withdraw(running.Id, actor, "申请人撤回还款");
            item.Cancel();
            repository.Update(item);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => item.SetStatus(previousStatus));
    }

    public void ApplyApproval(OaCashAdvanceRepayment item)
    {
        if (item.Status == OaCashAdvanceRepaymentStatus.Approved) return;
        if (item.Status != OaCashAdvanceRepaymentStatus.Submitted) throw new InvalidOperationException("只有已提交还款才能批准。 ");
        var advance = cashAdvanceService.Get(item.CashAdvanceId) ?? throw new InvalidOperationException("关联借款不存在或已被删除。 ");
        EnsureAdvanceMatches(advance, item.ApplicantUserId, item.Amount);
        var previousRepaymentStatus = item.Status;
        var previousAdvanceAmount = advance.SettledAmount;
        var previousAdvanceStatus = advance.Status;
        void Core()
        {
            cashAdvanceService.ApplyApprovedRepaymentSettlement(advance, item.ApplicantUserId, item.Amount);
            item.Approve();
            repository.Update(item);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ =>
        {
            item.SetStatus(previousRepaymentStatus);
            advance.SetSettledAmount(previousAdvanceAmount);
            advance.SetStatus(previousAdvanceStatus);
        });
    }

    public void ApplyRejection(OaCashAdvanceRepayment item, string? reason)
    {
        if (item.Status == OaCashAdvanceRepaymentStatus.Rejected) return;
        item.Reject(reason);
        repository.Update(item);
    }

    private void EnsureAdvanceAvailable(Guid cashAdvanceId, Guid applicantUserId, decimal amount)
    {
        var advance = cashAdvanceService.Get(cashAdvanceId) ?? throw new InvalidOperationException("关联借款不存在或已被删除。 ");
        EnsureAdvanceMatches(advance, applicantUserId, amount);
    }

    private static void EnsureAdvanceMatches(OaCashAdvance advance, Guid applicantUserId, decimal amount)
    {
        if (applicantUserId == Guid.Empty || advance.ApplicantUserId != applicantUserId) throw new UnauthorizedAccessException("当前用户不能操作其他员工的借款还款。 ");
        if (advance.Status is not (OaCashAdvanceStatus.Approved or OaCashAdvanceStatus.PartiallySettled)) throw new InvalidOperationException("只有已批准或部分结清的借款可以登记还款。 ");
        if (amount <= 0 || amount > advance.RemainingAmount) throw new InvalidOperationException("还款金额必须大于 0 且不能超过借款余额。 ");
    }

    private void EnsureSubmitReady(OaCashAdvanceRepayment item)
    {
        if (item.Status is not (OaCashAdvanceRepaymentStatus.Draft or OaCashAdvanceRepaymentStatus.Rejected)) throw new InvalidOperationException("当前状态不能提交还款。 ");
    }

    private void EnsureDocumentNoUnique(string documentNo, Guid ignoredId)
    {
        if (repository.List().Any(x => x.Id != ignoredId && x.DocumentNo.Equals(documentNo.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("还款单号已存在。 ");
    }

    private static void EnsureOwner(OaCashAdvanceRepayment item, Guid actorUserId)
    {
        if (actorUserId == Guid.Empty || item.ApplicantUserId != actorUserId) throw new UnauthorizedAccessException("当前用户不能操作其他员工的还款。 ");
    }

    private static void EnsureEditable(OaCashAdvanceRepayment item)
    {
        if (item.Status is not (OaCashAdvanceRepaymentStatus.Draft or OaCashAdvanceRepaymentStatus.Rejected)) throw new InvalidOperationException("只有草稿或已驳回还款可以编辑。 ");
    }
}
