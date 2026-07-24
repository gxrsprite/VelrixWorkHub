using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Lms;

/// <summary>许可证运营口径；全部基于当前申请和授权快照，不持久化统计副本。</summary>
public sealed class LmsLicenseOperationsSnapshotService(ILmsLicenseRepository repository)
{
    public LmsLicenseOperationsSnapshot Get(DateTime? now = null)
    {
        var currentTime = now ?? DateTime.Now;
        var requests = repository.ListRequests();
        var authorizations = repository.ListAuthorizations();
        var recentActivities = authorizations
            .SelectMany(authorization => repository.ListLifecycleEntries(authorization.Id).Select(entry => new LmsLicenseRecentActivity(
                authorization.Id,
                authorization.LicenseNo,
                entry.Action,
                entry.CurrentStatus,
                entry.Actor,
                entry.Reason,
                entry.OccurredAt)))
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.AuthorizationId)
            .Take(5)
            .ToArray();
        return new LmsLicenseOperationsSnapshot(
            requests.Count,
            requests.Count(x => x.Status == LmsLicenseRequestStatus.Submitted),
            requests.Count(x => x.Status == LmsLicenseRequestStatus.Approved),
            requests.Count(x => x.Status == LmsLicenseRequestStatus.Cancelled),
            authorizations.Count(x => x.GetEffectiveStatus(currentTime) == LmsLicenseStatus.Active),
            authorizations.Count(x => x.Status == LmsLicenseStatus.Active && x.ExpiresAt is DateTime expiresAt && expiresAt >= currentTime && expiresAt <= currentTime.AddDays(LmsLicenseExpiryReminderService.DefaultWarningDays)),
            authorizations.Count(x => x.GetEffectiveStatus(currentTime) == LmsLicenseStatus.Expired),
            authorizations.Count(x => x.Status == LmsLicenseStatus.Disabled),
            authorizations.Count(x => x.Status == LmsLicenseStatus.Revoked),
            recentActivities);
    }
}

public sealed record LmsLicenseOperationsSnapshot(
    int RequestCount,
    int PendingApprovalCount,
    int ApprovedRequestCount,
    int CancelledRequestCount,
    int ActiveAuthorizationCount,
    int ExpiringAuthorizationCount,
    int ExpiredAuthorizationCount,
    int DisabledAuthorizationCount,
    int RevokedAuthorizationCount,
    IReadOnlyList<LmsLicenseRecentActivity>? RecentActivities = null)
{
    public IReadOnlyList<LmsLicenseRecentActivity> Activities => RecentActivities ?? [];
}

public sealed record LmsLicenseRecentActivity(
    Guid AuthorizationId,
    string LicenseNo,
    LmsLicenseLifecycleAction Action,
    LmsLicenseStatus CurrentStatus,
    string Actor,
    string Reason,
    DateTime OccurredAt);
