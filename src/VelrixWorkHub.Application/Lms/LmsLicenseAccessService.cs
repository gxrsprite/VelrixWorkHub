using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Lms;

/// <summary>统一 LMS 读取范围：管理员看全量，普通用户只能看自己提交的申请及其授权。</summary>
public sealed class LmsLicenseAccessService(ILmsLicenseRepository repository)
{
    public IReadOnlyList<LmsLicenseRequest> ListRequests(string? actor, bool isAdministrator)
    {
        if (isAdministrator) return repository.ListRequests().OrderByDescending(x => x.CreatedAt).ToArray();
        if (string.IsNullOrWhiteSpace(actor)) return [];
        var normalizedActor = actor.Trim();
        return repository.ListRequests().Where(x => x.Applicant.Equals(normalizedActor, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.CreatedAt).ToArray();
    }

    public IReadOnlyList<LmsLicenseAuthorization> ListAuthorizations(string? actor, bool isAdministrator)
    {
        var visibleRequestIds = ListRequests(actor, isAdministrator).Select(x => x.Id).ToHashSet();
        return repository.ListAuthorizations().Where(x => isAdministrator || x.RequestId is Guid requestId && visibleRequestIds.Contains(requestId)).OrderByDescending(x => x.CreatedAt).ToArray();
    }

    public bool CanReadRequest(Guid requestId, string? actor, bool isAdministrator)
        => requestId != Guid.Empty && ListRequests(actor, isAdministrator).Any(x => x.Id == requestId);

    public bool CanReadAuthorization(Guid authorizationId, string? actor, bool isAdministrator)
        => authorizationId != Guid.Empty && ListAuthorizations(actor, isAdministrator).Any(x => x.Id == authorizationId);
}
