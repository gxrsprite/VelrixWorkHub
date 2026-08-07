using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Reports;

public sealed record CustomerPmsInsight(Guid CustomerId, int ProjectCount, int ActiveProjectCount, IReadOnlyList<PmsProject> Projects);

public static class CustomerPmsInsightService
{
    public static CustomerPmsInsight Build(Guid customerId, IEnumerable<PmsProject> projects)
    {
        var customerProjects = projects
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.Status == PmsProjectStatus.Active)
            .ThenBy(x => x.PlannedEnd)
            .ThenBy(x => x.Code)
            .ToArray();
        return new CustomerPmsInsight(customerId, customerProjects.Length, customerProjects.Count(x => x.Status == PmsProjectStatus.Active), customerProjects);
    }
}
