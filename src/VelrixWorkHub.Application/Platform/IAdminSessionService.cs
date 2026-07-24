using BootstrapBlazor.Components;

namespace AdminBlazor;

/// <summary>
/// 解析管理后台登录 Cookie 并加载当前用户、角色和菜单的会话契约。
/// </summary>
public interface IAdminSessionService
{
    Task<AdminSessionSnapshot?> LoadAsync(string? protectedCookie, CancellationToken cancellationToken = default);
}

public sealed record AdminSessionSnapshot(
    SysTenant? Tenant,
    SysUser User,
    IReadOnlyList<SysRole> Roles,
    IReadOnlyList<SysMenu> RoleMenus,
    IReadOnlyList<string> ButtonPaths);
