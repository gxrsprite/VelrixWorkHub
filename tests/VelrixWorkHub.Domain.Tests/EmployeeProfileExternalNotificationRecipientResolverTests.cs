using VelrixWorkHub.Application.Employees;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class EmployeeProfileExternalNotificationRecipientResolverTests
{
    [Fact]
    public void Resolve_ReturnsConfiguredChannelsOnlyForEnabledEmployedUser()
    {
        var userId = Guid.CreateVersion7();
        var profileRepository = new ProfileRepository();
        profileRepository.Add(new OaEmployeeProfile(userId, phone: "13800138000", email: "admin@example.com", status: OaEmploymentStatus.Employed, weComUserId: "wecom-admin", dingTalkUserId: "dingtalk-admin"));
        var resolver = new EmployeeProfileExternalNotificationRecipientResolver(
            new EmployeeDirectoryService(new DirectoryRepository(new EmployeeDirectoryEntry(userId, "admin", "管理员", null, null, true, null, null))),
            new EmployeeProfileService(profileRepository));

        var recipients = resolver.Resolve(new WorkNotification("ADMIN", WorkNotificationKind.System, "系统消息", "内容", null, "system:recipient"));

        Assert.Equal(4, recipients.Count);
        Assert.Contains(recipients, item => item.Channel == ExternalNotificationChannel.Email && item.Address == "admin@example.com");
        Assert.Contains(recipients, item => item.Channel == ExternalNotificationChannel.Sms && item.Address == "13800138000");
        Assert.Contains(recipients, item => item.Channel == ExternalNotificationChannel.WeCom && item.Address == "wecom-admin");
        Assert.Contains(recipients, item => item.Channel == ExternalNotificationChannel.DingTalk && item.Address == "dingtalk-admin");
    }

    [Fact]
    public void Resolve_SkipsDisabledOrNonEmployedUser()
    {
        var disabledId = Guid.CreateVersion7();
        var suspendedId = Guid.CreateVersion7();
        var profiles = new ProfileRepository();
        profiles.Add(new OaEmployeeProfile(disabledId, email: "disabled@example.com", status: OaEmploymentStatus.Employed));
        profiles.Add(new OaEmployeeProfile(suspendedId, email: "suspended@example.com", status: OaEmploymentStatus.Suspended));
        var resolver = new EmployeeProfileExternalNotificationRecipientResolver(
            new EmployeeDirectoryService(new DirectoryRepository(
                new EmployeeDirectoryEntry(disabledId, "disabled", "停用用户", null, null, false, null, null),
                new EmployeeDirectoryEntry(suspendedId, "suspended", "停职用户", null, null, true, null, null))),
            new EmployeeProfileService(profiles));

        Assert.Empty(resolver.Resolve(new WorkNotification("disabled", WorkNotificationKind.System, "系统消息", "内容", null, "system:disabled")));
        Assert.Empty(resolver.Resolve(new WorkNotification("suspended", WorkNotificationKind.System, "系统消息", "内容", null, "system:suspended")));
    }

    [Fact]
    public void Resolve_SkipsInvalidMailAndPhoneButKeepsOtherControlledChannels()
    {
        var userId = Guid.CreateVersion7();
        var profiles = new ProfileRepository();
        profiles.Add(new OaEmployeeProfile(userId, phone: "invalid-phone", email: "invalid-email", status: OaEmploymentStatus.Employed, weComUserId: "wecom-admin"));
        var resolver = new EmployeeProfileExternalNotificationRecipientResolver(
            new EmployeeDirectoryService(new DirectoryRepository(new EmployeeDirectoryEntry(userId, "admin", "管理员", null, null, true, null, null))),
            new EmployeeProfileService(profiles));

        var recipients = resolver.Resolve(new WorkNotification("admin", WorkNotificationKind.System, "系统消息", "内容", null, "system:invalid-contacts"));

        var recipient = Assert.Single(recipients);
        Assert.Equal(ExternalNotificationChannel.WeCom, recipient.Channel);
        Assert.Equal("wecom-admin", recipient.Address);
    }

    [Fact]
    public void Resolve_NormalizesPhoneForStableExternalAddressDedupe()
    {
        var userId = Guid.CreateVersion7();
        var profiles = new ProfileRepository();
        profiles.Add(new OaEmployeeProfile(userId, phone: "+86 138-0013-8000", status: OaEmploymentStatus.Employed));
        var resolver = new EmployeeProfileExternalNotificationRecipientResolver(
            new EmployeeDirectoryService(new DirectoryRepository(new EmployeeDirectoryEntry(userId, "admin", "管理员", null, null, true, null, null))),
            new EmployeeProfileService(profiles));

        var recipient = Assert.Single(resolver.Resolve(new WorkNotification("admin", WorkNotificationKind.System, "系统消息", "内容", null, "system:normalized-phone")));

        Assert.Equal(ExternalNotificationChannel.Sms, recipient.Channel);
        Assert.Equal("+8613800138000", recipient.Address);
    }

    private sealed class DirectoryRepository(params EmployeeDirectoryEntry[] users) : IEmployeeDirectoryRepository
    {
        public IReadOnlyList<EmployeeDirectoryEntry> List() => users;
        public IReadOnlyList<EmployeeDirectoryOrganization> ListOrganizations() => [];
    }

    private sealed class ProfileRepository : IOaEmployeeProfileRepository
    {
        private readonly List<OaEmployeeProfile> profiles = [];
        public IReadOnlyList<OaEmployeeProfile> List() => profiles;
        public OaEmployeeProfile? Get(Guid userId) => profiles.SingleOrDefault(profile => profile.UserId == userId);
        public void Add(OaEmployeeProfile profile) => profiles.Add(profile);
        public void Update(OaEmployeeProfile profile) { }
    }
}
