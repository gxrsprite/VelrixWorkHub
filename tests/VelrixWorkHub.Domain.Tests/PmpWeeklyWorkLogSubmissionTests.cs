using VelrixWorkHub.Domain;
using VelrixWorkHub.Infrastructure.PmpProjects;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PmpWeeklyWorkLogSubmissionTests
{
    [Fact]
    public void WeeklySubmission_UsesImmutableWeekSnapshotAndApprovalLifecycle()
    {
        var monday = new DateOnly(2026, 7, 20);
        var item = new PmpWeeklyWorkLogSubmission(Guid.CreateVersion7(), "项目经理", monday, "[{\"date\":\"2026-07-20\",\"hours\":8}]", 8m);

        item.Submit("project.manager", new DateTime(2026, 7, 21, 9, 0, 0));
        item.Approve();

        Assert.Equal(PmpWeeklyWorkLogSubmissionStatus.Approved, item.Status);
        Assert.Equal(8m, item.TotalHours);
        Assert.Throws<InvalidOperationException>(() => item.Withdraw());
    }

    [Fact]
    public void WeeklySubmission_RequiresMondayNonEmptySnapshotAndReasonForRejection()
    {
        Assert.Throws<ArgumentException>(() => new PmpWeeklyWorkLogSubmission(Guid.CreateVersion7(), "项目经理", new DateOnly(2026, 7, 21), "[{}]", 1m));
        var item = new PmpWeeklyWorkLogSubmission(Guid.CreateVersion7(), "项目经理", new DateOnly(2026, 7, 20), "[{}]", 1m);
        item.Submit("project.manager", DateTime.Now);
        Assert.Throws<ArgumentException>(() => item.Reject(""));
        item.Reject("请补充任务说明。");
        Assert.Equal(PmpWeeklyWorkLogSubmissionStatus.Rejected, item.Status);
    }

    [Fact]
    public void WeeklySubmission_RequiresValidJsonArraySnapshot()
    {
        Assert.Throws<ArgumentException>(() => new PmpWeeklyWorkLogSubmission(Guid.CreateVersion7(), "项目经理", new DateOnly(2026, 7, 20), "not-json", 1m));
        Assert.Throws<ArgumentException>(() => new PmpWeeklyWorkLogSubmission(Guid.CreateVersion7(), "项目经理", new DateOnly(2026, 7, 20), "{}", 1m));
    }

    [Fact]
    public void ActiveWeekKey_IsCaseInsensitiveAndOnlyRetainedForActiveApprovalStates()
    {
        var projectId = Guid.CreateVersion7();
        var weekStart = new DateOnly(2026, 7, 20);

        var submitted = PmpWeeklyWorkLogSubmissionSchemaMigration.GetActiveWeekKey(projectId, " 项目经理 ", weekStart, PmpWeeklyWorkLogSubmissionStatus.Submitted);
        var approved = PmpWeeklyWorkLogSubmissionSchemaMigration.GetActiveWeekKey(projectId, "项目经理", weekStart, PmpWeeklyWorkLogSubmissionStatus.Approved);

        Assert.Equal(submitted, approved);
        Assert.Null(PmpWeeklyWorkLogSubmissionSchemaMigration.GetActiveWeekKey(projectId, "项目经理", weekStart, PmpWeeklyWorkLogSubmissionStatus.Rejected));
        Assert.Null(PmpWeeklyWorkLogSubmissionSchemaMigration.GetActiveWeekKey(projectId, "项目经理", weekStart, PmpWeeklyWorkLogSubmissionStatus.Withdrawn));
        Assert.True(PmpWeeklyWorkLogSubmissionSchemaMigration.IsActiveWeekUniquenessViolation(new InvalidOperationException(PmpWeeklyWorkLogSubmissionSchemaMigration.ActiveWeekKeyUniqueIndex)));
    }
}
