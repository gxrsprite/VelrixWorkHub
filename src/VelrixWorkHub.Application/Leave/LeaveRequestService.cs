using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Leave;

public interface IOaLeaveRequestRepository
{
    IReadOnlyList<OaLeaveRequest> List(Guid? userId = null);
    OaLeaveRequest? Get(Guid id);
    void Add(OaLeaveRequest request);
    void Update(OaLeaveRequest request);
}

public interface IOaLeaveRequestWorkflowApprover
{
    void ApplyApproval(OaLeaveRequest request);
    void ApplyRejection(OaLeaveRequest request, string? reason);
}

public sealed class LeaveRequestService(
    IOaLeaveRequestRepository repository,
    WorkflowBindingService? bindings = null,
    IWorkflowTransactionBoundary? transactions = null,
    LeaveBalanceService? balances = null,
    LeaveCalendarService? calendar = null) : IOaLeaveRequestWorkflowApprover
{
    public IReadOnlyList<OaLeaveRequest> ListMine(Guid userId) => userId == Guid.Empty ? [] : repository.List(userId).OrderByDescending(item => item.StartAt).ToArray();

    public OaLeaveRequest Create(Guid userId, OaLeaveType leaveType, DateTime startAt, DateTime endAt, string reason, string? otherInfo)
    {
        var request = new OaLeaveRequest(userId, leaveType, startAt, endAt, reason, otherInfo, DateTime.Now);
        repository.Add(request);
        return request;
    }

    public void Edit(OaLeaveRequest request, Guid actorUserId, OaLeaveType leaveType, DateTime startAt, DateTime endAt, string reason, string? otherInfo)
    {
        EnsureOwner(request, actorUserId);
        if (request.Status is not (OaLeaveRequestStatus.Draft or OaLeaveRequestStatus.Rejected)) throw new InvalidOperationException("只有草稿或已驳回请假申请才能编辑。");
        request.Edit(leaveType, startAt, endAt, reason, otherInfo);
        repository.Update(request);
    }

    public void Submit(OaLeaveRequest request, Guid actorUserId)
    {
        EnsureOwner(request, actorUserId);
        EnsureSubmitReady(request);
        var previousStatus = request.Status;
        void Core()
        {
            balances?.ReserveForSubmission(request);
            request.Submit(DateTime.Now);
            repository.Update(request);
        }

        if (transactions is not null)
        {
            transactions.Execute(Core, _ => request.SetStatus(previousStatus));
            return;
        }

        try { Core(); }
        catch
        {
            request.SetStatus(previousStatus);
            balances?.ReleaseForRequest(request);
            throw;
        }
    }

    public void SubmitAndStartWorkflow(OaLeaveRequest request, Guid actorUserId, string startedBy)
    {
        EnsureOwner(request, actorUserId);
        if (bindings is null) throw new InvalidOperationException("请假审批服务未配置。");
        EnsureSubmitReady(request);
        var previousStatus = request.Status;
        void Core()
        {
            balances?.ReserveForSubmission(request);
            request.Submit(DateTime.Now);
            repository.Update(request);
            bindings.StartOrGet(WorkflowBindingCodes.LeaveApproval, nameof(OaLeaveRequest), request.Id, startedBy: startedBy);
        }
        if (transactions is not null)
        {
            transactions.Execute(Core, _ => request.SetStatus(previousStatus));
            return;
        }

        try { Core(); }
        catch
        {
            request.SetStatus(previousStatus);
            balances?.ReleaseForRequest(request);
            throw;
        }
    }

    public void Cancel(OaLeaveRequest request, Guid actorUserId)
    {
        EnsureOwner(request, actorUserId);
        var previousStatus = request.Status;
        void Core()
        {
            balances?.ReleaseForRequest(request);
            request.Cancel();
            repository.Update(request);
        }

        if (transactions is not null)
        {
            transactions.Execute(Core, _ => request.SetStatus(previousStatus));
            return;
        }

        try { Core(); }
        catch
        {
            request.SetStatus(previousStatus);
            if (previousStatus == OaLeaveRequestStatus.Submitted) balances?.ReserveForSubmission(request);
            throw;
        }
    }

    public void Cancel(OaLeaveRequest request, Guid actorUserId, string actor)
    {
        EnsureOwner(request, actorUserId);
        var running = bindings?.List(nameof(OaLeaveRequest), request.Id)
            .SingleOrDefault(x => x.Status == WorkflowInstanceStatus.Running);
        var previousStatus = request.Status;
        void Core()
        {
            if (running is not null) bindings!.Withdraw(running.Id, actor, "申请人撤回请假申请");
            balances?.ReleaseForRequest(request);
            request.Cancel();
            repository.Update(request);
        }
        if (transactions is not null)
        {
            transactions.Execute(Core, _ => request.SetStatus(previousStatus));
            return;
        }

        try { Core(); }
        catch
        {
            request.SetStatus(previousStatus);
            if (previousStatus == OaLeaveRequestStatus.Submitted) balances?.ReserveForSubmission(request);
            throw;
        }
    }

    public void ApplyApproval(OaLeaveRequest request)
    {
        if (request.Status == OaLeaveRequestStatus.Approved) return;
        var previousStatus = request.Status;
        void Core()
        {
            balances?.ConsumeForApproval(request);
            calendar?.CreateForApproval(request);
            request.Approve();
            repository.Update(request);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => request.SetStatus(previousStatus));
    }

    public void ApplyRejection(OaLeaveRequest request, string? reason)
    {
        if (request.Status == OaLeaveRequestStatus.Rejected) return;
        var previousStatus = request.Status;
        void Core()
        {
            balances?.ReleaseForRequest(request);
            request.Reject(reason);
            repository.Update(request);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => request.SetStatus(previousStatus));
    }

    private void EnsureSubmitReady(OaLeaveRequest request)
    {
        if (request.Status is not (OaLeaveRequestStatus.Draft or OaLeaveRequestStatus.Rejected))
            throw new InvalidOperationException("只有草稿或已驳回请假申请才能提交。");
        if (repository.List(request.UserId).Any(item => item.Id != request.Id && item.Status is (OaLeaveRequestStatus.Submitted or OaLeaveRequestStatus.Approved) && item.Overlaps(request.StartAt, request.EndAt)))
            throw new InvalidOperationException("该时间段已有提交中或已批准的请假申请。");
    }

    private static void EnsureOwner(OaLeaveRequest request, Guid actorUserId)
    {
        if (actorUserId == Guid.Empty || request.UserId != actorUserId)
            throw new UnauthorizedAccessException("当前用户不能操作其他员工的请假申请。");
    }
}
