using VelrixWorkHub.Application.Employees;
using VelrixWorkHub.Application.Offboarding;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class OffboardingServiceTests
{
    [Fact]
    public void Create_RequiresActiveProfileAndIsUnique()
    {
        var profiles = new ProfileRepository();
        var userId = Guid.CreateVersion7();
        var employeeProfiles = new EmployeeProfileService(profiles);
        var service = new OffboardingService(new OffboardingRepository(), employeeProfiles);

        Assert.Throws<InvalidOperationException>(() => service.Create(userId, DateOnly.FromDateTime(DateTime.Today), "个人发展", null, null));
        employeeProfiles.Save(userId, "E001", null, null, "项目经理", DateOnly.FromDateTime(DateTime.Today.AddYears(-1)), OaEmploymentStatus.Employed, null, "admin", true);
        service.Create(userId, DateOnly.FromDateTime(DateTime.Today), "个人发展", "已完成交接", null);

        Assert.Throws<InvalidOperationException>(() => service.Create(userId, DateOnly.FromDateTime(DateTime.Today), "重复办理", null, null));
    }

    [Fact]
    public void Complete_RequiresAllChecklistItemsAndResignsProfile()
    {
        var profiles = new ProfileRepository();
        var userId = Guid.CreateVersion7();
        var employeeProfiles = new EmployeeProfileService(profiles);
        employeeProfiles.Save(userId, "E002", "13800001234", null, "财务经理", null, OaEmploymentStatus.Employed, null, "admin", true);
        var repository = new OffboardingRepository();
        var service = new OffboardingService(repository, employeeProfiles);
        var record = service.Create(userId, DateOnly.FromDateTime(DateTime.Today), "合同到期", null, null);

        service.UpdateChecklist(record, true, true, true, true, false);
        Assert.Throws<InvalidOperationException>(() => service.Complete(record, "admin", true));
        Assert.Equal(OaEmploymentStatus.Employed, employeeProfiles.Get(userId)!.Status);

        service.UpdateChecklist(record, true, true, true, true, true);
        service.Complete(record, "admin", true);

        Assert.Equal(OaOffboardingStatus.Completed, record.Status);
        Assert.Equal(OaEmploymentStatus.Resigned, employeeProfiles.Get(userId)!.Status);
        Assert.Throws<InvalidOperationException>(() => service.UpdateChecklist(record, true, true, true, true, true));
        Assert.Throws<InvalidOperationException>(() => service.Edit(record, record.LastWorkDate, "修改", null, null));
    }

    [Fact]
    public void Complete_RequiresPermissionAndRecordValidatesOtherInfo()
    {
        var userId = Guid.CreateVersion7();
        Assert.Throws<ArgumentException>(() => new OaOffboardingRecord(userId, DateOnly.FromDateTime(DateTime.Today), "", null, null, DateTime.Now));
        Assert.Throws<ArgumentException>(() => new OaOffboardingRecord(userId, DateOnly.FromDateTime(DateTime.Today), "个人发展", null, "[]", DateTime.Now));

        var profiles = new ProfileRepository();
        var employeeProfiles = new EmployeeProfileService(profiles);
        employeeProfiles.Save(userId, "E003", null, null, null, null, OaEmploymentStatus.Employed, null, "admin", true);
        var service = new OffboardingService(new OffboardingRepository(), employeeProfiles);
        var record = service.Create(userId, DateOnly.FromDateTime(DateTime.Today), "个人发展", null, null);
        service.UpdateChecklist(record, true, true, true, true, true);

        Assert.Throws<UnauthorizedAccessException>(() => service.Complete(record, "admin", false));
        Assert.Equal(OaEmploymentStatus.Employed, employeeProfiles.Get(userId)!.Status);
    }

    [Fact]
    public void Complete_DisablesPlatformAccountAndKeepsAuditSnapshot()
    {
        var profiles = new ProfileRepository();
        var accounts = new AccountLifecycleService();
        var userId = Guid.CreateVersion7();
        var employeeProfiles = new EmployeeProfileService(profiles);
        employeeProfiles.Save(userId, "E004", null, null, "采购专员", null, OaEmploymentStatus.Employed, null, "admin", true);
        var service = new OffboardingService(new OffboardingRepository(), employeeProfiles, accounts);
        var record = service.Create(userId, DateOnly.FromDateTime(DateTime.Today), "合同到期", null, null);
        service.UpdateChecklist(record, true, true, true, true, true);

        service.Complete(record, "admin", true);

        Assert.Equal(userId, accounts.DisabledUserId);
        Assert.Equal("admin", accounts.Actor);
        Assert.True(record.AccountDisabled);
        Assert.Equal("admin", record.AccountDisabledBy);
        Assert.Contains("离职办理完成", record.AccountDisableReason);
        Assert.Equal(OaEmploymentStatus.Resigned, employeeProfiles.Get(userId)!.Status);
    }

    [Fact]
    public void Complete_AccountDisableFailureStopsEmployeeStatusTransition()
    {
        var profiles = new ProfileRepository();
        var userId = Guid.CreateVersion7();
        var employeeProfiles = new EmployeeProfileService(profiles);
        employeeProfiles.Save(userId, "E005", null, null, "销售专员", null, OaEmploymentStatus.Employed, null, "admin", true);
        var service = new OffboardingService(new OffboardingRepository(), employeeProfiles, new AccountLifecycleService { ThrowOnDisable = true });
        var record = service.Create(userId, DateOnly.FromDateTime(DateTime.Today), "个人发展", null, null);
        service.UpdateChecklist(record, true, true, true, true, true);

        Assert.Throws<InvalidOperationException>(() => service.Complete(record, "admin", true));
        Assert.Equal(OaEmploymentStatus.Employed, employeeProfiles.Get(userId)!.Status);
        Assert.NotEqual(OaOffboardingStatus.Completed, record.Status);
        Assert.False(record.AccountDisabled);
    }

    [Fact]
    public void Complete_BlocksWhenApplicationRiskProviderReportsOpenItems()
    {
        var profiles = new ProfileRepository();
        var userId = Guid.CreateVersion7();
        var employeeProfiles = new EmployeeProfileService(profiles);
        employeeProfiles.Save(userId, "E006", null, null, "项目专员", null, OaEmploymentStatus.Employed, null, "admin", true);
        var service = new OffboardingService(new OffboardingRepository(), employeeProfiles, risks: new RiskProvider());
        var record = service.Create(userId, DateOnly.FromDateTime(DateTime.Today), "个人发展", null, null);
        service.UpdateChecklist(record, true, true, true, true, true);

        var error = Assert.Throws<InvalidOperationException>(() => service.Complete(record, "admin", true));

        Assert.Contains("未处理离职风险", error.Message);
        Assert.Equal(OaEmploymentStatus.Employed, employeeProfiles.Get(userId)!.Status);
        Assert.NotEqual(OaOffboardingStatus.Completed, record.Status);
    }

    private sealed class ProfileRepository : IOaEmployeeProfileRepository
    {
        private readonly Dictionary<Guid, OaEmployeeProfile> profiles = [];
        public IReadOnlyList<OaEmployeeProfile> List() => profiles.Values.ToArray();
        public OaEmployeeProfile? Get(Guid userId) => profiles.GetValueOrDefault(userId);
        public void Add(OaEmployeeProfile profile) => profiles.Add(profile.UserId, profile);
        public void Update(OaEmployeeProfile profile) => profiles[profile.UserId] = profile;
    }

    private sealed class AccountLifecycleService : IEmployeeAccountLifecycleService
    {
        public Guid? DisabledUserId { get; private set; }
        public string? Actor { get; private set; }
        public bool ThrowOnDisable { get; init; }

        public void Disable(Guid userId, string actor, string reason)
        {
            if (ThrowOnDisable) throw new InvalidOperationException("平台账号停用失败。");
            DisabledUserId = userId;
            Actor = actor;
        }
    }

    private sealed class RiskProvider : IOaOffboardingRiskProvider
    {
        public IReadOnlyList<OaOffboardingRiskItem> List(Guid userId) => [new(OaOffboardingRiskKind.CashAdvance, Guid.CreateVersion7(), "CA-001", "借款/备用金未结清", "/Oa/CashAdvance")];
    }

    private sealed class OffboardingRepository : IOaOffboardingRepository
    {
        private readonly List<OaOffboardingRecord> records = [];
        public IReadOnlyList<OaOffboardingRecord> List() => records;
        public OaOffboardingRecord? Get(Guid id) => records.FirstOrDefault(item => item.Id == id);
        public OaOffboardingRecord? GetByUser(Guid userId) => records.FirstOrDefault(item => item.UserId == userId);
        public void Add(OaOffboardingRecord record) => records.Add(record);
        public void Update(OaOffboardingRecord record) { }
    }
}
