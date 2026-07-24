using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Lms;

public sealed class LmsLicenseReplacementRequestService(
    ILmsLicenseReplacementRequestRepository repository,
    LmsLicenseService licenses,
    WorkflowBindingService? bindings = null,
    IWorkflowTransactionBoundary? transactions = null,
    WorkflowTaskService? tasks = null,
    LmsLicenseAccessService? access = null)
{
    public IReadOnlyList<LmsLicenseReplacementRequest> List(string? applicant = null)
        => repository.List().Where(x => string.IsNullOrWhiteSpace(applicant) || x.Applicant.Equals(applicant.Trim(), StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.CreatedAt).ToArray();

    public IReadOnlyList<LmsLicenseReplacementRequest> ListVisible(string? actor, bool isAdministrator)
    {
        var all = repository.List();
        if (isAdministrator) return all.OrderByDescending(x => x.CreatedAt).ToArray();
        if (string.IsNullOrWhiteSpace(actor)) return [];
        var normalizedActor = actor.Trim();
        var assignedIds = tasks?.List(assignee: normalizedActor)
            .Where(x => x.BusinessType.Equals(nameof(LmsLicenseReplacementRequest), StringComparison.OrdinalIgnoreCase))
            .Select(x => x.BusinessId)
            .ToHashSet() ?? [];
        return all
            .Where(x => x.Applicant.Equals(normalizedActor, StringComparison.OrdinalIgnoreCase) || assignedIds.Contains(x.Id))
            .OrderByDescending(x => x.CreatedAt)
            .ToArray();
    }

    public bool CanRead(Guid requestId, string? actor, bool isAdministrator)
        => requestId != Guid.Empty && ListVisible(actor, isAdministrator).Any(x => x.Id == requestId);

    public LmsLicenseReplacementRequest Create(string requestNo, Guid originalAuthorizationId, LmsLicenseReplacementKind kind, Guid? targetMachineId, string licenseNo, string externalLicense, DateTime? expiresAt, string? otherInfo, string applicant, string reason, bool isAdministrator = false)
    {
        if (repository.List().Any(x => x.RequestNo.Equals(requestNo.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("授权替代申请编号已存在。");
        if (repository.List().Any(x => x.OriginalAuthorizationId == originalAuthorizationId && x.Status == LmsLicenseReplacementRequestStatus.Submitted)) throw new InvalidOperationException("该原授权已有审批中的替代申请。");
        var original = licenses.ListAuthorizations().SingleOrDefault(x => x.Id == originalAuthorizationId) ?? throw new InvalidOperationException("原授权不存在。");
        EnsureOriginalAuthorizationAccess(original, applicant, isAdministrator);
        if (original.Status != LmsLicenseStatus.Active) throw new InvalidOperationException("只有有效授权可以发起替代申请。");
        if (expiresAt is DateTime expires && expires <= DateTime.Now) throw new InvalidOperationException("替代授权的到期时间必须晚于当前时间。");
        var item = new LmsLicenseReplacementRequest(requestNo, originalAuthorizationId, kind, targetMachineId, licenseNo, externalLicense, expiresAt, otherInfo, applicant, reason, DateTime.Now);
        repository.Add(item);
        return item;
    }

    public void SubmitAndStartWorkflow(LmsLicenseReplacementRequest item, string startedBy)
    {
        EnsureNoOtherSubmittedRequest(item);
        if (bindings is null) throw new InvalidOperationException("授权替代申请审批服务未配置。");
        var previousStatus = item.Status;
        void SubmitCore()
        {
            item.Submit();
            repository.Update(item);
            bindings.StartOrGet(WorkflowBindingCodes.LmsLicenseReplacementApproval, nameof(LmsLicenseReplacementRequest), item.Id, startedBy: startedBy);
        }
        if (transactions is null) SubmitCore(); else transactions.Execute(SubmitCore, _ => item.SetStatus(previousStatus));
    }

    public void ResubmitAfterWithdrawal(LmsLicenseReplacementRequest item, string startedBy)
    {
        if (bindings is null) throw new InvalidOperationException("授权替代申请审批服务未配置。");
        if (item.Status != LmsLicenseReplacementRequestStatus.Submitted) throw new InvalidOperationException("当前授权替代申请不能重新提交。");
        var latest = bindings.List(nameof(LmsLicenseReplacementRequest), item.Id).OrderByDescending(x => x.StartedAt).FirstOrDefault();
        if (latest?.Status != WorkflowInstanceStatus.Cancelled) throw new InvalidOperationException("只有已撤回的授权替代申请可以重新提交。");
        EnsureNoOtherSubmittedRequest(item);
        var previousStatus = item.Status;
        void ResubmitCore()
        {
            item.SetStatus(LmsLicenseReplacementRequestStatus.Withdrawn);
            repository.Update(item);
            bindings.Resubmit(WorkflowBindingCodes.LmsLicenseReplacementApproval, nameof(LmsLicenseReplacementRequest), item.Id, startedBy: startedBy);
            item.Submit();
            repository.Update(item);
        }
        if (transactions is null) ResubmitCore(); else transactions.Execute(ResubmitCore, _ => item.SetStatus(previousStatus));
    }

    private void EnsureNoOtherSubmittedRequest(LmsLicenseReplacementRequest item)
    {
        if (repository.List().Any(x => x.Id != item.Id && x.OriginalAuthorizationId == item.OriginalAuthorizationId && x.Status == LmsLicenseReplacementRequestStatus.Submitted))
            throw new InvalidOperationException("该原授权已有审批中的替代申请。");
    }

    private void EnsureOriginalAuthorizationAccess(LmsLicenseAuthorization original, string applicant, bool isAdministrator)
    {
        if (isAdministrator) return;
        if (access is not null)
        {
            if (!access.CanReadAuthorization(original.Id, applicant, isAdministrator))
                throw new InvalidOperationException("当前用户无权发起该授权的替代申请。");
            return;
        }

        if (original.RequestId is not Guid requestId) return;
        var request = licenses.ListRequests().FirstOrDefault(x => x.Id == requestId);
        if (request is null || !request.Applicant.Equals(applicant.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("当前用户无权发起该授权的替代申请。");
    }
}
