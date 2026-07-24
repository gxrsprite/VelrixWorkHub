using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Reports;

public sealed record CustomerPmpInsight(Guid CustomerId, int ProjectCount, int ActiveProjectCount, IReadOnlyList<PmpProject> Projects);

public static class CustomerPmpInsightService
{
    public static CustomerPmpInsight Build(Guid customerId, IEnumerable<PmpProject> projects)
    {
        var customerProjects = projects
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.Status == PmpProjectStatus.Active)
            .ThenBy(x => x.PlannedEnd)
            .ThenBy(x => x.Code)
            .ToArray();
        return new CustomerPmpInsight(customerId, customerProjects.Length, customerProjects.Count(x => x.Status == PmpProjectStatus.Active), customerProjects);
    }
}
