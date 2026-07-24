using VelrixWorkHub.Application.Employees;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Notifications;

/// <summary>从启用、在职人员的受控员工档案解析站外通知地址。</summary>
public sealed class EmployeeProfileExternalNotificationRecipientResolver(
    EmployeeDirectoryService directory,
    EmployeeProfileService profiles) : IExternalNotificationRecipientResolver
{
    public IReadOnlyList<ExternalNotificationRecipient> Resolve(WorkNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var person = directory.List(status: EmployeeDirectoryStatus.Enabled)
            .FirstOrDefault(item => item.Username.Equals(notification.Recipient, StringComparison.OrdinalIgnoreCase));
        if (person is null) return [];
        var profile = profiles.Get(person.UserId);
        if (profile?.Status != OaEmploymentStatus.Employed) return [];

        var recipients = new List<ExternalNotificationRecipient>();
        AddIfValid(recipients, ExternalNotificationChannel.Email, profile.Email);
        AddIfValid(recipients, ExternalNotificationChannel.Sms, profile.Phone);
        AddIfValid(recipients, ExternalNotificationChannel.WeCom, profile.WeComUserId);
        AddIfValid(recipients, ExternalNotificationChannel.DingTalk, profile.DingTalkUserId);
        return recipients;
    }

    private static void AddIfValid(List<ExternalNotificationRecipient> recipients, ExternalNotificationChannel channel, string? address)
    {
        if (ExternalNotificationRecipient.TryCreate(channel, address, out var recipient)) recipients.Add(recipient);
    }
}
