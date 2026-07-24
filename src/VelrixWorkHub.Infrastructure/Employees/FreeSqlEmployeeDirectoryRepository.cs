using BootstrapBlazor.Components;
using FreeSql;
using VelrixWorkHub.Application.Employees;

namespace VelrixWorkHub.Infrastructure.Employees;

public sealed class FreeSqlEmployeeDirectoryRepository(IFreeSql fsql) : IEmployeeDirectoryRepository
{
    public IReadOnlyList<EmployeeDirectoryEntry> List()
    {
        var organizations = fsql.Select<SysOrg>().ToList()
            .Where(item => item.Id != Guid.Empty)
            .ToDictionary(item => item.Id, item => item.Label);
        var roles = ListRoles().ToDictionary(item => item.Id);
        var roleIdsByUser = fsql.Select<SysRoleUser>().ToList()
            .Where(item => item.UserId != Guid.Empty && roles.ContainsKey(item.RoleId))
            .GroupBy(item => item.UserId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<EmployeeDirectoryRole>)group.Select(item => roles[item.RoleId]).OrderBy(item => item.Name).ToArray());

        return fsql.Select<SysUser>()
            .OrderBy(item => item.Nickname)
            .ToList()
            .Select(item => new EmployeeDirectoryEntry(
                item.Id,
                item.Username?.Trim() ?? string.Empty,
                string.IsNullOrWhiteSpace(item.Nickname) ? item.Username?.Trim() ?? "未命名" : item.Nickname.Trim(),
                item.OrgId == Guid.Empty ? null : item.OrgId,
                item.OrgId != Guid.Empty && organizations.TryGetValue(item.OrgId, out var organizationName) ? organizationName : null,
                item.IsEnabled,
                string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim(),
                item.LoginTime == default ? null : item.LoginTime,
                roleIdsByUser.TryGetValue(item.Id, out var userRoles) ? userRoles : []))
            .ToArray();
    }

    public IReadOnlyList<EmployeeDirectoryOrganization> ListOrganizations() =>
        fsql.Select<SysOrg>()
            .Where(item => item.IsEnabled)
            .OrderBy(item => item.Label)
            .ToList()
            .Where(item => item.Id != Guid.Empty && !string.IsNullOrWhiteSpace(item.Label))
            .Select(item => new EmployeeDirectoryOrganization(item.Id, item.Label.Trim()))
            .ToArray();

    public IReadOnlyList<EmployeeDirectoryRole> ListRoles() =>
        fsql.Select<SysRole>()
            .OrderBy(item => item.Name)
            .ToList()
            .Where(item => item.Id != Guid.Empty && !string.IsNullOrWhiteSpace(item.Name))
            .Select(item => new EmployeeDirectoryRole(item.Id, item.Name.Trim()))
            .ToArray();
}
