using BootstrapBlazor.Components;
using FreeSql;
using VelrixWorkHub.Application.Platform;

namespace AdminBlazor.Services;

public sealed class AdminAuthorizationService(IFreeSql fsql) : IAdminPermissionService
{
    public async Task<IReadOnlyList<SysRole>> LoadUserRolesAsync(Guid userId)
    {
        var roleIds = (await fsql.Select<SysRoleUser>()
                .Where(item => item.UserId == userId)
                .ToListAsync())
            .Select(item => item.RoleId)
            .Distinct()
            .ToArray();

        return roleIds.Length == 0
            ? []
            : await fsql.Select<SysRole>().Where(role => roleIds.Contains(role.Id)).ToListAsync();
    }

    public async Task<IReadOnlyList<SysMenu>> LoadAuthorizedMenusAsync(Guid userId, IReadOnlyList<SysRole>? roles = null)
    {
        roles ??= await LoadUserRolesAsync(userId);
        if (roles.Count == 0)
            return [];

        var menus = roles.Any(role => role.IsAdministrator)
            ? await fsql.Select<SysMenu>()
                .Where(menu => menu.Type == SysMenuType.菜单 && !menu.IsHidden)
                .OrderBy(menu => menu.Sort)
                .ToListAsync()
            : await LoadRoleMenusWithAncestorsAsync(roles.Select(role => role.Id).ToArray());

        return BuildMenuTree(menus);
    }

    public async Task<IReadOnlyList<string>> LoadAuthorizedButtonPathsAsync(IReadOnlyList<SysRole> roles)
    {
        if (roles.Count == 0)
            return [];

        if (roles.Any(role => role.IsAdministrator))
        {
            return (await fsql.Select<SysMenu>()
                    .Where(menu => menu.Type == SysMenuType.按钮 && !menu.IsHidden && !string.IsNullOrWhiteSpace(menu.Path))
                    .ToListAsync())
                .Select(menu => menu.Path)
                .Distinct()
                .OrderBy(path => path)
                .ToArray();
        }

        var roleIds = roles.Select(role => role.Id).ToArray();
        var menuIds = (await fsql.Select<SysRoleMenu>()
                .Where(item => roleIds.Contains(item.RoleId))
                .ToListAsync())
            .Select(item => item.MenuId)
            .Distinct()
            .ToArray();
        if (menuIds.Length == 0)
            return [];

        return (await fsql.Select<SysMenu>()
                .Where(menu => menu.Type == SysMenuType.按钮
                    && menuIds.Contains(menu.Id)
                    && !menu.IsHidden
                    && !string.IsNullOrWhiteSpace(menu.Path))
                .ToListAsync())
            .Select(menu => menu.Path)
            .Distinct()
            .OrderBy(path => path)
            .ToArray();
    }

    public async Task<bool> CanAccessMenuAsync(Guid userId, string path)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(path))
            return false;

        var roles = await LoadUserRolesAsync(userId);
        if (roles.Count == 0)
            return false;
        if (roles.Any(role => role.IsAdministrator))
            return true;

        var menus = await LoadAuthorizedMenusAsync(userId, roles);
        return menus.SelectMany(menu => FlattenMenus(new[] { menu })).Any(menu =>
            string.Equals(menu.Path, path, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<IReadOnlyList<SysMenu>> LoadRoleMenusWithAncestorsAsync(Guid[] roleIds)
    {
        if (roleIds.Length == 0)
            return [];

        var authorizedIds = (await fsql.Select<SysRoleMenu>()
                .Where(item => roleIds.Contains(item.RoleId))
                .ToListAsync())
            .Select(item => item.MenuId)
            .ToHashSet();
        if (authorizedIds.Count == 0)
            return [];

        var allMenus = await fsql.Select<SysMenu>()
            .Where(menu => menu.Type == SysMenuType.菜单 && !menu.IsHidden)
            .OrderBy(menu => menu.Sort)
            .ToListAsync();
        var byId = allMenus.ToDictionary(menu => menu.Id);
        var includedIds = new HashSet<Guid>();
        foreach (var id in authorizedIds)
        {
            var currentId = id;
            while (currentId != Guid.Empty && byId.TryGetValue(currentId, out var menu))
            {
                if (!includedIds.Add(menu.Id))
                    break;
                currentId = menu.ParentId;
            }
        }

        return allMenus.Where(menu => includedIds.Contains(menu.Id)).ToList();
    }

    private static IReadOnlyList<SysMenu> BuildMenuTree(IReadOnlyList<SysMenu> menus)
    {
        var lookup = menus.ToLookup(menu => menu.ParentId);
        var roots = menus.Where(menu => menu.ParentId == Guid.Empty).OrderBy(menu => menu.Sort).ToList();
        foreach (var root in roots)
            PopulateChildren(root);
        return roots;

        void PopulateChildren(SysMenu parent)
        {
            parent.Children = lookup[parent.Id].OrderBy(menu => menu.Sort).ToList();
            foreach (var child in parent.Children)
                PopulateChildren(child);
        }
    }

    private static IEnumerable<SysMenu> FlattenMenus(IEnumerable<SysMenu> menus)
    {
        foreach (var menu in menus)
        {
            yield return menu;
            foreach (var child in FlattenMenus(menu.Children ?? []))
                yield return child;
        }
    }
}
