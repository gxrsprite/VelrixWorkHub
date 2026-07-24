using VelrixWorkHub.Application.Leave;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Overtime;

public interface IOaOvertimeRequestRepository
{
    IReadOnlyList<OaOvertimeRequest> List(Guid? userId = null);
    OaOvertimeRequest? Get(Guid id);
    void Add(OaOvertimeRequest request);
    void Update(OaOvertimeRequest request);
}

public interface IOaOvertimeRequestWorkflowApprover
{
    void ApplyApproval(OaOvertimeRequest request);
    void ApplyRejection(OaOvertimeRequest request, string? reason);
}

public sealed class OvertimeRequestService(
    IOaOvertimeRequestRepository repository,
    LeaveRequestService? leaveRequests = null,
    WorkflowBindingService? bindings = null,
    IWorkflowTransactionBoundary? transactions = null) : IOaOvertimeRequestWorkflowApprover
{
    public IReadOnlyList<OaOvertimeRequest> ListMine(Guid userId) => userId == Guid.Empty ? [] : repository.List(userId).OrderByDescending(item => item.StartAt).ToArray();
    public OaOvertimeRequest? Get(Guid id) => repository.Get(id);

    public OaOvertimeRequest Create(Guid userId, DateTime startAt, DateTime endAt, string reason, string? otherInfo)
    {
        var request = new OaOvertimeRequest(userId, startAt, endAt, reason, otherInfo, DateTime.Now);
        repository.Add(request);
        return request;
    }

    public void Edit(OaOvertimeRequest request, Guid actorUserId, DateTime startAt, DateTime endAt, string reason, string? otherInfo)
    {
        EnsureOwner(request, actorUserId);
        if (request.Status is not (OaOvertimeRequestStatus.Draft or OaOvertimeRequestStatus.Rejected)) throw new InvalidOperationException("只有草稿或已驳回加班申请才能编辑。");
        request.Edit(startAt, endAt, reason, otherInfo);
        repository.Update(request);
    }

    public void Submit(OaOvertimeRequest request, Guid actorUserId)
    {
        EnsureOwner(request, actorUserId);
        EnsureSubmitReady(request);
        request.Submit(DateTime.Now);
        repository.Update(request);
    }

    public void SubmitAndStartWorkflow(OaOvertimeRequest request, Guid actorUserId, string startedBy)
    {
        EnsureOwner(request, actorUserId);
        if (bindings is null) throw new InvalidOperationException("加班审批服务未配置。");
        EnsureSubmitReady(request);
        var previousStatus = request.Status;
        void Core()
        {
            request.Submit(DateTime.Now);
            repository.Update(request);
            bindings.StartOrGet(WorkflowBindingCodes.OvertimeApproval, nameof(OaOvertimeRequest), request.Id, startedBy: startedBy);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => request.SetStatus(previousStatus));
    }

    public void Cancel(OaOvertimeRequest request, Guid actorUserId, string actor)
    {
        EnsureOwner(request, actorUserId);
        var running = bindings?.List(nameof(OaOvertimeRequest), request.Id).SingleOrDefault(x => x.Status == WorkflowInstanceStatus.Running);
        var previousStatus = request.Status;
        void Core()
        {
            if (running is not null) bindings!.Withdraw(running.Id, actor, "申请人撤回加班申请");
            request.Cancel();
            repository.Update(request);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => request.SetStatus(previousStatus));
    }

    public void ApplyApproval(OaOvertimeRequest request)
    {
        if (request.Status == OaOvertimeRequestStatus.Approved) return;
        request.Approve();
        repository.Update(request);
    }

    public void ApplyRejection(OaOvertimeRequest request, string? reason)
    {
        if (request.Status == OaOvertimeRequestStatus.Rejected) return;
        request.Reject(reason);
        repository.Update(request);
    }

    private void EnsureSubmitReady(OaOvertimeRequest request)
    {
        if (request.Status is not (OaOvertimeRequestStatus.Draft or OaOvertimeRequestStatus.Rejected)) throw new InvalidOperationException("只有草稿或已驳回加班申请才能提交。");
        if (leaveRequests?.ListMine(request.UserId).Any(item => item.Status is OaLeaveRequestStatus.Submitted or OaLeaveRequestStatus.Approved && item.Overlaps(request.StartAt, request.EndAt)) == true)
            throw new InvalidOperationException("该时间段已有提交中或已批准的请假申请。");
    }

    private static void EnsureOwner(OaOvertimeRequest request, Guid actorUserId)
    {
        if (actorUserId == Guid.Empty || request.UserId != actorUserId) throw new UnauthorizedAccessException("当前用户不能操作其他员工的加班申请。");
    }
}
