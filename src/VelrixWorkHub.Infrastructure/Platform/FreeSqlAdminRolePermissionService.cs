using BootstrapBlazor.Components;
using FreeSql;
using System.Text.Json;
using VelrixWorkHub.Application.Platform;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Platform;

public sealed class FreeSqlAdminRolePermissionService : IAdminRolePermissionService
{
    private const string SubjectType = "RolePermission";
    private const string Action = "Replace";
    private readonly IFreeSql _fsql;
    private readonly IAdminPermissionAuditService _audit;

    public FreeSqlAdminRolePermissionService(IFreeSql fsql, IAdminPermissionAuditService? audit = null)
    {
        _fsql = fsql;
        _audit = audit ?? new FreeSqlAdminPermissionAuditService(fsql);
    }

    public async Task<IReadOnlyList<SysMenu>> LoadAssignableMenusAsync()
    {
        return await _fsql.Select<SysMenu>()
            .OrderBy(menu => menu.Sort)
            .OrderBy(menu => menu.Label)
            .ToListAsync();
    }

    public async Task<AdminRolePermissionSnapshot?> LoadRolePermissionsAsync(Guid roleId)
    {
        if (roleId == Guid.Empty)
            return null;

        var role = await _fsql.Select<SysRole>()
            .Where(item => item.Id == roleId)
            .FirstAsync();
        if (role is null)
            return null;

        var menuIds = (await _fsql.Select<SysRoleMenu>()
                .Where(item => item.RoleId == roleId)
                .ToListAsync())
            .Select(item => item.MenuId)
            .Distinct()
            .ToArray();

        return new AdminRolePermissionSnapshot(role.Id, role.IsAdministrator, menuIds);
    }

    public async Task<AdminRolePermissionSaveResult> SaveRolePermissionsAsync(
        Guid roleId,
        IReadOnlyCollection<Guid> menuIds,
        Guid? actorUserId = null,
        string? actorUserName = null)
    {
        if (roleId == Guid.Empty)
            return new(false, "角色编号不能为空。", 0);

        var roleExists = await _fsql.Select<SysRole>()
            .Where(item => item.Id == roleId)
            .AnyAsync();
        if (!roleExists)
            return new(false, "角色不存在或已被删除。", 0);

        var normalizedIds = menuIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        if (normalizedIds.Length > 0)
        {
            var existingIds = (await _fsql.Select<SysMenu>()
                    .Where(menu => normalizedIds.Contains(menu.Id))
                    .ToListAsync())
                .Select(menu => menu.Id)
                .ToHashSet();
            if (existingIds.Count != normalizedIds.Length)
                return new(false, "授权菜单中包含不存在的菜单或按钮。", 0);
        }

        var beforeIds = (await _fsql.Select<SysRoleMenu>()
                .Where(item => item.RoleId == roleId)
                .ToListAsync())
            .Select(item => item.MenuId)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
        var afterIds = normalizedIds.OrderBy(id => id).ToArray();
        if (beforeIds.SequenceEqual(afterIds))
            return new(true, null, normalizedIds.Length);

        var beforeData = JsonSerializer.Serialize(new PermissionAuditPayload(beforeIds), JsonSerializationDefaults.CreateWeb());
        var afterData = JsonSerializer.Serialize(new PermissionAuditPayload(afterIds), JsonSerializationDefaults.CreateWeb());

        _fsql.Transaction(() =>
        {
            _fsql.Delete<SysRoleMenu>()
                .Where(item => item.RoleId == roleId)
                .ExecuteAffrows();

            if (normalizedIds.Length > 0)
            {
                _fsql.Insert(normalizedIds.Select(menuId => new SysRoleMenu
                {
                    RoleId = roleId,
                    MenuId = menuId
                })).ExecuteAffrows();
            }

            _audit.Record(roleId, SubjectType, Action, beforeData, afterData, actorUserId, actorUserName);
        });

        return new(true, null, normalizedIds.Length);
    }

    private sealed record PermissionAuditPayload(IReadOnlyList<Guid> MenuIds);
}
