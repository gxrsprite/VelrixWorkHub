using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Lms;

/// <summary>将授权到期事件投递到统一通知平台；扫描自身不改变授权状态。</summary>
public sealed class LmsLicenseExpiryReminderService(ILmsLicenseRepository repository, NotificationService notifications)
{
    public const int DefaultWarningDays = 30;

    public LmsLicenseExpiryScanResult Scan(DateTime now, int warningDays = DefaultWarningDays)
    {
        if (warningDays is < 1 or > 365) throw new ArgumentOutOfRangeException(nameof(warningDays), "预警天数必须在 1 到 365 天之间。");
        var requests = repository.ListRequests().ToDictionary(x => x.Id);
        var expiring = 0;
        var grace = 0;
        var expired = 0;
        var skipped = 0;
        foreach (var authorization in repository.ListAuthorizations())
        {
            if (authorization.Status != LmsLicenseStatus.Active || authorization.ExpiresAt is not DateTime expiresAt || authorization.RequestId is not Guid requestId || !requests.TryGetValue(requestId, out var request) || string.IsNullOrWhiteSpace(request.Applicant))
            {
                skipped++;
                continue;
            }

            var href = $"/Lms/License?requestId={request.Id}";
            if (expiresAt < now)
            {
                if (authorization.IsWithinGracePeriod(now))
                {
                    notifications.Publish(request.Applicant, WorkNotificationKind.Reminder, "许可证进入宽限期", $"授权 {authorization.LicenseNo}（{authorization.ProductName}）已于 {expiresAt:yyyy-MM-dd} 到期，当前处于 {authorization.GracePeriodDays} 天宽限期内。", href, DedupeKey(authorization, "grace", expiresAt), now);
                    grace++;
                }
                else
                {
                    notifications.Publish(request.Applicant, WorkNotificationKind.Reminder, "许可证已到期", $"授权 {authorization.LicenseNo}（{authorization.ProductName}）已于 {expiresAt:yyyy-MM-dd} 到期且宽限期已结束。", href, DedupeKey(authorization, "expired", expiresAt), now);
                    expired++;
                }
            }
            else if (expiresAt <= now.AddDays(warningDays))
            {
                notifications.Publish(request.Applicant, WorkNotificationKind.Reminder, "许可证即将到期", $"授权 {authorization.LicenseNo}（{authorization.ProductName}）将于 {expiresAt:yyyy-MM-dd} 到期。", href, DedupeKey(authorization, "expiring", expiresAt), now);
                expiring++;
            }
            else
            {
                skipped++;
            }
        }
        return new LmsLicenseExpiryScanResult(expiring, grace, expired, skipped);
    }

    private static string DedupeKey(LmsLicenseAuthorization authorization, string kind, DateTime expiresAt)
        => $"lms-license:{authorization.Id}:{kind}:{expiresAt:yyyyMMdd}";
}

public sealed record LmsLicenseExpiryScanResult(int ExpiringNotifications, int GracePeriodNotifications, int ExpiredNotifications, int SkippedAuthorizations);
