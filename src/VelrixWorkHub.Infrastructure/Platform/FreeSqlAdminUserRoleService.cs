using BootstrapBlazor.Components;
using FreeSql;
using System.Text.Json;
using VelrixWorkHub.Application.Platform;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Platform;

public sealed class FreeSqlAdminUserRoleService : IAdminUserRoleService
{
    private const string SubjectType = "UserRole";
    private const string Action = "Replace";
    private readonly IFreeSql _fsql;
    private readonly IAdminPermissionAuditService _audit;

    public FreeSqlAdminUserRoleService(IFreeSql fsql, IAdminPermissionAuditService? audit = null)
    {
        _fsql = fsql;
        _audit = audit ?? new FreeSqlAdminPermissionAuditService(fsql);
    }

    public async Task<IReadOnlyList<SysRole>> LoadAssignableRolesAsync()
    {
        return await _fsql.Select<SysRole>()
            .OrderByDescending(role => role.IsAdministrator)
            .OrderBy(role => role.Name)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Guid>> LoadUserRoleIdsAsync(Guid userId)
    {
        if (userId == Guid.Empty)
            return [];

        return (await _fsql.Select<SysRoleUser>()
                .Where(item => item.UserId == userId)
                .ToListAsync())
            .Select(item => item.RoleId)
            .Distinct()
            .ToArray();
    }

    public async Task<AdminUserRoleSaveResult> SaveUserRolesAsync(
        Guid userId,
        IReadOnlyCollection<Guid> roleIds,
        Guid? actorUserId = null,
        string? actorUserName = null)
    {
        if (userId == Guid.Empty)
            return new(false, "用户编号不能为空。", 0);

        var userExists = await _fsql.Select<SysUser>()
            .Where(user => user.Id == userId)
            .AnyAsync();
        if (!userExists)
            return new(false, "用户不存在或已被删除。", 0);

        var normalizedIds = roleIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        var roles = normalizedIds.Length == 0
            ? []
            : await _fsql.Select<SysRole>()
                .Where(role => normalizedIds.Contains(role.Id))
                .ToListAsync();
        if (roles.Count != normalizedIds.Length)
            return new(false, "角色分配中包含不存在的角色。", 0);

        var currentRoleIds = await LoadUserRoleIdsAsync(userId);
        var currentHasAdministrator = currentRoleIds.Count > 0
            && await _fsql.Select<SysRole>()
                .Where(role => currentRoleIds.Contains(role.Id) && role.IsAdministrator)
                .AnyAsync();
        var assigningAdministrator = roles.Any(role => role.IsAdministrator);
        if (currentHasAdministrator && !assigningAdministrator)
        {
            var administratorRoleIds = (await _fsql.Select<SysRole>()
                    .Where(role => role.IsAdministrator)
                    .ToListAsync())
                .Select(role => role.Id)
                .ToArray();
            var otherAdministratorAssignments = administratorRoleIds.Length == 0
                ? 0
                : (await _fsql.Select<SysRoleUser>()
                        .Where(item => administratorRoleIds.Contains(item.RoleId) && item.UserId != userId)
                        .ToListAsync())
                    .Select(item => item.UserId)
                    .Distinct()
                    .Count();
            if (otherAdministratorAssignments == 0)
                return new(false, "系统必须至少保留一名管理员，不能移除最后一名管理员的全部管理员角色。", 0);
        }

        var beforeIds = currentRoleIds.OrderBy(id => id).ToArray();
        var afterIds = normalizedIds.OrderBy(id => id).ToArray();
        if (beforeIds.SequenceEqual(afterIds))
            return new(true, null, normalizedIds.Length);

        var beforeData = JsonSerializer.Serialize(new PermissionAuditPayload(beforeIds), JsonSerializationDefaults.CreateWeb());
        var afterData = JsonSerializer.Serialize(new PermissionAuditPayload(afterIds), JsonSerializationDefaults.CreateWeb());

        _fsql.Transaction(() =>
        {
            _fsql.Delete<SysRoleUser>()
                .Where(item => item.UserId == userId)
                .ExecuteAffrows();

            if (normalizedIds.Length > 0)
            {
                _fsql.Insert(normalizedIds.Select(roleId => new SysRoleUser
                {
                    UserId = userId,
                    RoleId = roleId
                })).ExecuteAffrows();
            }

            _audit.Record(userId, SubjectType, Action, beforeData, afterData, actorUserId, actorUserName);
        });

        return new(true, null, normalizedIds.Length);
    }

    private sealed record PermissionAuditPayload(IReadOnlyList<Guid> RoleIds);
}
