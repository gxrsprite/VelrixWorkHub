using VelrixWorkHub.Application.Employees;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class EmployeeProfileTests
{
    [Fact]
    public void Profile_TrimsValuesAndPreservesOtherInfo()
    {
        var profile = new OaEmployeeProfile(
            Guid.NewGuid(),
            " EMP-001 ",
            " 13800001234 ",
            " alice@example.com ",
            " 后端工程师 ",
            new DateOnly(2026, 7, 20),
            OaEmploymentStatus.Employed,
            "{\"level\":\"P6\"}",
            " wecom-alice ",
            " dingtalk-alice ");

        Assert.Equal("EMP-001", profile.EmployeeNo);
        Assert.Equal("13800001234", profile.Phone);
        Assert.Equal("后端工程师", profile.PositionTitle);
        Assert.Equal("wecom-alice", profile.WeComUserId);
        Assert.Equal("dingtalk-alice", profile.DingTalkUserId);
        Assert.Equal("{\"level\":\"P6\"}", profile.OtherInfo);
    }

    [Fact]
    public void Profile_RejectsInvalidOtherInfoAndEmptyUser()
    {
        Assert.Throws<ArgumentException>(() => new OaEmployeeProfile(Guid.NewGuid(), otherInfo: "[]"));
        Assert.Throws<ArgumentException>(() => new OaEmployeeProfile(Guid.Empty));
    }

    [Fact]
    public void Service_CreatesAndUpdatesProfileOnlyWithPermission()
    {
        var repository = new TestRepository();
        var service = new EmployeeProfileService(repository);
        var userId = Guid.NewGuid();

        Assert.Throws<UnauthorizedAccessException>(() => service.Save(userId, "EMP-001", null, null, null, null, OaEmploymentStatus.Employed, null, "admin", false));
        var created = service.Save(userId, " EMP-001 ", "13800001234", null, "工程师", null, OaEmploymentStatus.Employed, "{}", "admin", true);
        var updated = service.Save(userId, "EMP-001", null, "a@example.com", "高级工程师", null, OaEmploymentStatus.Suspended, "{\"team\":\"A\"}", "admin", true);

        Assert.Same(created, updated);
        Assert.Equal(OaEmploymentStatus.Suspended, updated.Status);
        Assert.Equal("高级工程师", updated.PositionTitle);
        Assert.Equal(1, repository.AddedCount);
        Assert.Equal(1, repository.UpdatedCount);
    }

    private sealed class TestRepository : IOaEmployeeProfileRepository
    {
        private readonly List<OaEmployeeProfile> profiles = [];
        public int AddedCount { get; private set; }
        public int UpdatedCount { get; private set; }
        public IReadOnlyList<OaEmployeeProfile> List() => profiles;
        public OaEmployeeProfile? Get(Guid userId) => profiles.SingleOrDefault(item => item.UserId == userId);
        public void Add(OaEmployeeProfile profile) { profiles.Add(profile); AddedCount++; }
        public void Update(OaEmployeeProfile profile) => UpdatedCount++;
    }
}
