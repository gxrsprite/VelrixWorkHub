using FreeSql;
using BootstrapBlazor.Components;
using VelrixWorkHub.Application.Workflow;

namespace VelrixWorkHub.Infrastructure.Workflow;

public sealed class FreeSqlWorkflowOrganizationApproverLookup(IFreeSql fsql) : IWorkflowOrganizationApproverLookup
{
    public IReadOnlyList<string> FindUsernames(IReadOnlyCollection<string> organizationNames)
    {
        ArgumentNullException.ThrowIfNull(organizationNames);
        var names = organizationNames.Select(x => x?.Trim() ?? string.Empty).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (names.Length == 0) return [];
        var normalizedNames = names.Select(x => x.ToUpperInvariant()).ToArray();
        var organizationIds = fsql.Select<SysOrg>().Where(x => normalizedNames.Contains(x.Label.ToUpper()) && x.IsEnabled).ToList().Select(x => x.Id).ToArray();
        if (organizationIds.Length == 0) return [];
        return fsql.Select<SysUser>().Where(x => organizationIds.Contains(x.OrgId) && x.IsEnabled).ToList()
            .Select(x => x.Username?.Trim() ?? string.Empty)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
