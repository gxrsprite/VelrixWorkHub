using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Assets;

public interface IOaAssetRequestRepository
{
    IReadOnlyList<OaAssetRequest> List(Guid? applicantUserId = null, Guid? assetId = null);
    OaAssetRequest? Get(Guid id);
    void Add(OaAssetRequest request);
    void Update(OaAssetRequest request);
}

public interface IOaAssetRequestWorkflowApprover
{
    void ApplyApproval(OaAssetRequest request, string? actorName = null);
    void ApplyRejection(OaAssetRequest request, string? reason);
}

public sealed class AssetRequestService(
    IOaAssetRequestRepository requests,
    AssetService assets,
    WorkflowBindingService? bindings = null,
    IWorkflowTransactionBoundary? transactions = null) : IOaAssetRequestWorkflowApprover
{
    public IReadOnlyList<OaAssetRequest> ListMine(Guid applicantUserId)
        => applicantUserId == Guid.Empty ? [] : requests.List(applicantUserId).OrderByDescending(item => item.CreatedAt).ToArray();

    public IReadOnlyList<OaAssetRequest> List() => requests.List().OrderByDescending(item => item.CreatedAt).ToArray();
    public OaAssetRequest? Get(Guid id) => id == Guid.Empty ? null : requests.Get(id);

    public OaAssetRequest Create(Guid applicantUserId, string applicantName, Guid assetId, string reason, string? otherInfo)
    {
        EnsureApplicant(applicantUserId);
        EnsureAvailable(assetId);
        var request = new OaAssetRequest(assetId, applicantUserId, applicantName, reason, otherInfo, DateTime.Now);
        requests.Add(request);
        return request;
    }

    public void Edit(OaAssetRequest request, Guid actorUserId, string applicantName, string reason, string? otherInfo)
    {
        EnsureOwner(request, actorUserId);
        EnsureAvailable(request.AssetId);
        request.Edit(applicantName, reason, otherInfo);
        requests.Update(request);
    }

    public void SubmitAndStartWorkflow(OaAssetRequest request, Guid actorUserId, string startedBy)
    {
        EnsureOwner(request, actorUserId);
        if (bindings is null) throw new InvalidOperationException("资产申请审批服务未配置。");
        EnsureSubmitReady(request);
        var previousStatus = request.Status;
        var previousAssignmentId = request.AssignmentId;
        var previousRejectionReason = request.RejectionReason;
        var previousApprovedAt = request.ApprovedAt;
        var previousSubmittedAt = request.SubmittedAt;
        void Core()
        {
            request.Submit(DateTime.Now);
            requests.Update(request);
            bindings.StartOrGet(WorkflowBindingCodes.AssetRequestApproval, nameof(OaAssetRequest), request.Id, startedBy: startedBy);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => request.SetStatusForRecovery(previousStatus, previousAssignmentId, previousRejectionReason, previousApprovedAt, previousSubmittedAt));
    }

    public void Cancel(OaAssetRequest request, Guid actorUserId, string actor)
    {
        EnsureOwner(request, actorUserId);
        var running = bindings?.List(nameof(OaAssetRequest), request.Id).SingleOrDefault(item => item.Status == WorkflowInstanceStatus.Running);
        var previousStatus = request.Status;
        var previousAssignmentId = request.AssignmentId;
        var previousRejectionReason = request.RejectionReason;
        var previousApprovedAt = request.ApprovedAt;
        var previousSubmittedAt = request.SubmittedAt;
        void Core()
        {
            if (running is not null) bindings!.Withdraw(running.Id, actor, "申请人撤回资产申请");
            request.Cancel();
            requests.Update(request);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => request.SetStatusForRecovery(previousStatus, previousAssignmentId, previousRejectionReason, previousApprovedAt, previousSubmittedAt));
    }

    public void ApplyApproval(OaAssetRequest request, string? actorName = null)
    {
        if (request.Status == OaAssetRequestStatus.Approved) return;
        if (request.Status != OaAssetRequestStatus.Submitted) throw new InvalidOperationException("只有已提交的资产申请可以批准。");
        EnsureSubmitReady(request);
        var asset = assets.Get(request.AssetId) ?? throw new InvalidOperationException("申请资产不存在或已被删除。");
        var previousAssignmentId = request.AssignmentId;
        var previousStatus = request.Status;
        var previousRejectionReason = request.RejectionReason;
        var previousApprovedAt = request.ApprovedAt;
        var previousSubmittedAt = request.SubmittedAt;
        OaAssetAssignment? assignment = null;
        void Core()
        {
            assignment = assets.Assign(asset, request.ApplicantUserId, true);
            request.Approve(assignment.Id, DateTime.Now);
            requests.Update(request);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => request.SetStatusForRecovery(previousStatus, previousAssignmentId, previousRejectionReason, previousApprovedAt, previousSubmittedAt));
    }

    public void ApplyRejection(OaAssetRequest request, string? reason)
    {
        if (request.Status == OaAssetRequestStatus.Rejected) return;
        if (request.Status != OaAssetRequestStatus.Submitted) throw new InvalidOperationException("只有已提交的资产申请可以驳回。");
        request.Reject(reason);
        requests.Update(request);
    }

    private void EnsureSubmitReady(OaAssetRequest request)
    {
        EnsureAvailable(request.AssetId);
        if (requests.List(assetId: request.AssetId).Any(item => item.Id != request.Id && item.Status is OaAssetRequestStatus.Submitted or OaAssetRequestStatus.Approved))
            throw new InvalidOperationException("该资产已有审批中或已批准的领用申请。");
    }

    private void EnsureAvailable(Guid assetId)
    {
        var asset = assets.Get(assetId) ?? throw new InvalidOperationException("申请资产不存在或已被删除。");
        if (asset.Status != OaAssetStatus.Available) throw new InvalidOperationException("只有可用资产才能申请领用。");
        if (assets.ListAssignments(assetId).Any(item => item.Status == OaAssetAssignmentStatus.Active))
            throw new InvalidOperationException("该资产已有未归还领用记录。");
    }

    private static void EnsureApplicant(Guid userId)
    {
        if (userId == Guid.Empty) throw new UnauthorizedAccessException("当前用户不能为空。");
    }

    private static void EnsureOwner(OaAssetRequest request, Guid actorUserId)
    {
        if (actorUserId == Guid.Empty || request.ApplicantUserId != actorUserId)
            throw new UnauthorizedAccessException("当前用户不能操作其他员工的资产申请。");
    }
}
