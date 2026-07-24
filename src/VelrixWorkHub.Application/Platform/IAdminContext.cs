using BootstrapBlazor.Components;

namespace AdminBlazor;

/// <summary>
/// Blazor 页面使用的当前管理会话门面。具体的 Cookie、FreeSql 和 HTTP 上下文实现留在宿主侧。
/// </summary>
public interface IAdminContext
{
    SysTenant? Tenant { get; }

    SysUser? User { get; }

    IReadOnlyList<SysRole> Roles { get; }

    IReadOnlyList<SysMenu> RoleMenus { get; }

    Task InitAsync();

    bool AuthPath(string path);

    bool AuthButton(string buttonPath, string? buttonName = null);

    void SignOut();
}
