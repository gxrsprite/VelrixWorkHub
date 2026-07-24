using BootstrapBlazor.Components;

namespace VelrixWorkHub.Application.Platform;

public sealed record AdminRolePermissionSnapshot(
    Guid RoleId,
    bool IsAdministrator,
    IReadOnlyList<Guid> MenuIds);

public sealed record AdminRolePermissionSaveResult(
    bool Success,
    string? Error,
    int AssignedCount);

/// <summary>
/// 平台角色权限管理用例。
/// 角色页只通过该契约读取可分配菜单和保存角色菜单/按钮授权。
/// </summary>
public interface IAdminRolePermissionService
{
    Task<IReadOnlyList<SysMenu>> LoadAssignableMenusAsync();

    Task<AdminRolePermissionSnapshot?> LoadRolePermissionsAsync(Guid roleId);

    Task<AdminRolePermissionSaveResult> SaveRolePermissionsAsync(
        Guid roleId,
        IReadOnlyCollection<Guid> menuIds,
        Guid? actorUserId = null,
        string? actorUserName = null);
}
