using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.ProcurementRequests;

public interface IOaProcurementRequestRepository
{
    IReadOnlyList<OaProcurementRequest> List(Guid? applicantUserId = null);
    OaProcurementRequest? Get(Guid id);
    void Add(OaProcurementRequest item);
    void Update(OaProcurementRequest item);
}

public interface IOaProcurementRequestLineRepository
{
    IReadOnlyList<OaProcurementRequestLine> List(Guid requestId);
    OaProcurementRequestLine? Get(Guid id);
    void Add(OaProcurementRequestLine item);
    void Remove(Guid id);
}

public interface IOaProcurementRequestWorkflowApprover
{
    void ApplyApproval(OaProcurementRequest item);
    void ApplyRejection(OaProcurementRequest item, string? reason);
}

public sealed class ProcurementRequestService(
    IOaProcurementRequestRepository repository,
    IOaProcurementRequestLineRepository lineRepository,
    WorkflowBindingService? bindings = null,
    IWorkflowTransactionBoundary? transactions = null,
    ProcurementBudgetService? budgets = null) : IOaProcurementRequestWorkflowApprover
{
    public IReadOnlyList<OaProcurementRequest> ListMine(Guid applicantUserId)
        => applicantUserId == Guid.Empty ? [] : repository.List(applicantUserId).OrderByDescending(x => x.CreatedAt).ToArray();

    public IReadOnlyList<OaProcurementRequest> List() => repository.List().OrderByDescending(x => x.CreatedAt).ToArray();
    public OaProcurementRequest? Get(Guid id) => repository.Get(id);
    public IReadOnlyList<OaProcurementRequestLine> ListLines(Guid requestId) => lineRepository.List(requestId).OrderBy(x => x.Id).ToArray();

    public OaProcurementRequest Create(Guid applicantUserId, string applicantName, string departmentName, string legalEntity,
        string documentNo, OaProcurementRequestType requestType, DateOnly requestDate, DateOnly requiredDate,
        Guid? projectId, string? budgetReference, string purpose, string? otherInfo)
    {
        EnsureDocumentNoUnique(documentNo, Guid.Empty);
        var item = new OaProcurementRequest(applicantUserId, applicantName, departmentName, legalEntity, documentNo,
            requestType, requestDate, requiredDate, projectId, budgetReference, purpose, otherInfo, DateTime.Now);
        repository.Add(item);
        return item;
    }

    public void Edit(OaProcurementRequest item, Guid actorUserId, string applicantName, string departmentName, string legalEntity,
        string documentNo, OaProcurementRequestType requestType, DateOnly requestDate, DateOnly requiredDate,
        Guid? projectId, string? budgetReference, string purpose, string? otherInfo)
    {
        EnsureOwner(item, actorUserId);
        EnsureEditable(item);
        EnsureDocumentNoUnique(documentNo, item.Id);
        if (requestType != item.RequestType && ListLines(item.Id).Count > 0) throw new InvalidOperationException("已有采购明细时不能切换申请类型，请先清空明细后再切换。");
        item.Edit(applicantName, departmentName, legalEntity, documentNo, requestType, requestDate, requiredDate, projectId, budgetReference, purpose, otherInfo);
        repository.Update(item);
    }

    public OaProcurementRequestLine AddLine(OaProcurementRequest item, Guid actorUserId, Guid? productId, string itemName,
        string materialCategory, string specification, decimal quantity, string unit, decimal estimatedUnitPrice, string? otherInfo)
    {
        EnsureOwner(item, actorUserId);
        EnsureEditable(item);
        EnsureProductBranch(item.RequestType, productId);
        var line = new OaProcurementRequestLine(item.Id, productId, itemName, materialCategory, specification, quantity, unit, estimatedUnitPrice, otherInfo);
        lineRepository.Add(line);
        Recalculate(item);
        return line;
    }

    public void RemoveLine(OaProcurementRequest item, Guid actorUserId, OaProcurementRequestLine line)
    {
        EnsureOwner(item, actorUserId);
        EnsureEditable(item);
        if (line.RequestId != item.Id) throw new InvalidOperationException("明细不属于当前采购申请。");
        lineRepository.Remove(line.Id);
        Recalculate(item);
    }

    public void Submit(OaProcurementRequest item, Guid actorUserId)
    {
        EnsureOwner(item, actorUserId);
        EnsureSubmitReady(item);
        var previousStatus = item.Status;
        void Core()
        {
            budgets?.ReserveForSubmission(item);
            item.Submit(DateTime.Now);
            repository.Update(item);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => item.SetStatusForRecovery(previousStatus));
    }

    public void SubmitAndStartWorkflow(OaProcurementRequest item, Guid actorUserId, string startedBy)
    {
        EnsureOwner(item, actorUserId);
        if (bindings is null) throw new InvalidOperationException("采购申请审批服务未配置。");
        EnsureSubmitReady(item);
        var previousStatus = item.Status;
        void Core()
        {
            budgets?.ReserveForSubmission(item);
            item.Submit(DateTime.Now);
            repository.Update(item);
            bindings.StartOrGet(WorkflowBindingCodes.ProcurementRequestApproval, nameof(OaProcurementRequest), item.Id, startedBy: startedBy);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => item.SetStatusForRecovery(previousStatus));
    }

    public void Cancel(OaProcurementRequest item, Guid actorUserId, string actor)
    {
        EnsureOwner(item, actorUserId);
        var running = bindings?.List(nameof(OaProcurementRequest), item.Id).SingleOrDefault(x => x.Status == WorkflowInstanceStatus.Running);
        var previousStatus = item.Status;
        void Core()
        {
            if (running is not null) bindings!.Withdraw(running.Id, actor, "申请人撤回采购申请");
            item.Cancel();
            budgets?.ReleaseForRequest(item);
            repository.Update(item);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => item.SetStatusForRecovery(previousStatus));
    }

    public void ApplyApproval(OaProcurementRequest item)
    {
        if (item.Status == OaProcurementRequestStatus.Approved) return;
        item.Approve();
        repository.Update(item);
    }

    public void ApplyRejection(OaProcurementRequest item, string? reason)
    {
        if (item.Status == OaProcurementRequestStatus.Rejected) return;
        item.Reject(reason);
        budgets?.ReleaseForRequest(item);
        repository.Update(item);
    }

    private void EnsureSubmitReady(OaProcurementRequest item)
    {
        EnsureEditableOrRejected(item);
        var lines = ListLines(item.Id);
        if (lines.Count == 0) throw new InvalidOperationException("采购申请至少需要一条明细。");
        foreach (var line in lines) EnsureProductBranch(item.RequestType, line.ProductId);
        Recalculate(item);
        if (item.EstimatedAmount <= 0) throw new InvalidOperationException("采购申请预计金额必须大于 0。");
    }

    private void Recalculate(OaProcurementRequest item)
    {
        item.SetEstimatedAmount(ListLines(item.Id).Sum(x => x.EstimatedAmount));
        repository.Update(item);
    }

    private static void EnsureProductBranch(OaProcurementRequestType requestType, Guid? productId)
    {
        if (requestType == OaProcurementRequestType.ProductRelated && productId is null)
            throw new InvalidOperationException("产品相关采购申请必须选择产品。");
        if (requestType != OaProcurementRequestType.ProductRelated && productId is not null)
            throw new InvalidOperationException("非产品相关采购申请不能绑定产品。");
    }

    private void EnsureDocumentNoUnique(string documentNo, Guid ignoredId)
    {
        if (repository.List().Any(x => x.Id != ignoredId && x.DocumentNo.Equals(documentNo.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("采购申请单号已存在。");
    }

    private static void EnsureOwner(OaProcurementRequest item, Guid actorUserId) { if (actorUserId == Guid.Empty || item.ApplicantUserId != actorUserId) throw new UnauthorizedAccessException("当前用户不能操作其他员工的采购申请。"); }
    private static void EnsureEditable(OaProcurementRequest item) { if (item.Status is not (OaProcurementRequestStatus.Draft or OaProcurementRequestStatus.Rejected)) throw new InvalidOperationException("只有草稿或已驳回采购申请可以编辑。"); }
    private static void EnsureEditableOrRejected(OaProcurementRequest item) { if (item.Status is not (OaProcurementRequestStatus.Draft or OaProcurementRequestStatus.Rejected)) throw new InvalidOperationException("当前状态不能提交采购申请。"); }
}

internal static class OaProcurementRequestRecoveryExtensions
{
    public static void SetStatusForRecovery(this OaProcurementRequest item, OaProcurementRequestStatus status) => item.SetStatus(status);
}
