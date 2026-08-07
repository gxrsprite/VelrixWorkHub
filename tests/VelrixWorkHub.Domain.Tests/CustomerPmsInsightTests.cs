using VelrixWorkHub.Application.Reports;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class CustomerPmsInsightTests
{
    [Fact]
    public void Build_OnlyIncludesCustomerProjectsAndCountsActiveProjects()
    {
        var customerId = Guid.CreateVersion7();
        var active = new PmsProject("PRJ-A", "进行中项目", customerId, null, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));
        active.SetStatus(PmsProjectStatus.Active);
        var completed = new PmsProject("PRJ-B", "已完成项目", customerId, null, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30));
        completed.SetStatus(PmsProjectStatus.Completed);
        var other = new PmsProject("PRJ-C", "其他客户项目", Guid.CreateVersion7(), null, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));

        var insight = CustomerPmsInsightService.Build(customerId, [other, completed, active]);

        Assert.Equal(2, insight.ProjectCount);
        Assert.Equal(1, insight.ActiveProjectCount);
        Assert.Equal([active.Id, completed.Id], insight.Projects.Select(x => x.Id).ToArray());
    }
}
