using AdminBlazor;
using AdminBlazor.Services;
using BootstrapBlazor.Components;
using FreeSql;
using VelrixWorkHub.Application.Platform;

namespace VelrixWorkHub.Infrastructure.Platform;

/// <summary>
/// FreeSql 管理会话实现。Cookie 解密、用户有效性和授权数据由平台服务统一加载。
/// </summary>
public sealed class AdminSessionService(
    IFreeSql fsql,
    AdminAuthCookieService authCookie,
    IAdminPermissionService permissions) : IAdminSessionService
{
    public async Task<AdminSessionSnapshot?> LoadAsync(string? protectedCookie, CancellationToken cancellationToken = default)
    {
        if (!authCookie.TryGetSession(protectedCookie, out var session))
            return null;

        cancellationToken.ThrowIfCancellationRequested();
        var user = await fsql.Select<SysUser>()
            .Where(item => item.Id == session.UserId && item.IsEnabled)
            .FirstAsync();
        if (user == null || user.AuthVersion != session.AuthVersion)
            return null;

        var tenant = await fsql.Select<SysTenant>().Where(item => item.Id == "main").FirstAsync();
        var roles = await permissions.LoadUserRolesAsync(user.Id);
        var menus = await permissions.LoadAuthorizedMenusAsync(user.Id, roles);
        var buttonPaths = await permissions.LoadAuthorizedButtonPathsAsync(roles);
        return new AdminSessionSnapshot(tenant, user, roles, menus, buttonPaths);
    }
}
