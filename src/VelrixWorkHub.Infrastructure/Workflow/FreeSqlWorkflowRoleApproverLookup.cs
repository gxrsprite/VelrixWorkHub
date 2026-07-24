using FreeSql;
using BootstrapBlazor.Components;
using VelrixWorkHub.Application.Workflow;

namespace VelrixWorkHub.Infrastructure.Workflow;

public sealed class FreeSqlWorkflowRoleApproverLookup(IFreeSql fsql) : IWorkflowRoleApproverLookup
{
    public IReadOnlyList<string> FindUsernames(IReadOnlyCollection<string> roleNames)
    {
        ArgumentNullException.ThrowIfNull(roleNames);
        var names = roleNames.Select(x => x?.Trim() ?? string.Empty).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (names.Length == 0) return [];
        var normalizedNames = names.Select(x => x.ToUpperInvariant()).ToArray();
        var roleIds = fsql.Select<SysRole>().Where(x => normalizedNames.Contains(x.Name.ToUpper())).ToList().Select(x => x.Id).ToArray();
        if (roleIds.Length == 0) return [];
        var userIds = fsql.Select<SysRoleUser>().Where(x => roleIds.Contains(x.RoleId)).ToList().Select(x => x.UserId).Distinct().ToArray();
        if (userIds.Length == 0) return [];
        return fsql.Select<SysUser>().Where(x => userIds.Contains(x.Id) && x.IsEnabled).ToList()
            .Select(x => x.Username?.Trim() ?? string.Empty)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
