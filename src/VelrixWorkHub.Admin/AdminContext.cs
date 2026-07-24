using System.Collections.Concurrent;
using BootstrapBlazor.Components;
using FreeSql;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using VelrixWorkHub.Application.Platform;

namespace AdminBlazor;

/// <summary>
/// 实时消息
/// </summary>
public class AdminMessageInfo
{
    public Guid ReceiveUserId { get; set; }
    public string? Content { get; set; }
    public DateTime Time { get; set; }
}

/// <summary>
/// 管理后台上下文 - 每个作用域一个实例
/// </summary>
public sealed class AdminContext : IDisposable, IAdminContext
{
    public class TabInfo
    {
        public string Key { get; set; } = "";
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public bool IsActive { get; set; }
        public int ComponentKey { get; set; }
        public int Sort { get; set; }
        public bool IsLoad { get; set; }
        public bool IsClosed { get; set; }
        public string? Exception { get; set; }
        public Type? PageType { get; set; }
    }

    private readonly FreeSqlCloud<string> _fsqlCloud;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly IAdminSessionService _sessionService;
    private readonly IAdminPermissionService _permissionService;
    private readonly HashSet<string> _authorizedButtonPaths = new(StringComparer.OrdinalIgnoreCase);

    public IServiceProvider Service { get; }
    public SysTenant? Tenant { get; private set; }
    public IFreeSql Orm => _fsqlCloud.Use("main");
    public SysUser? User { get; set; }
    public List<SysRole> Roles { get; set; } = new();
    public List<SysMenu> RoleMenus { get; set; } = new();
    IReadOnlyList<SysRole> IAdminContext.Roles => Roles;
    IReadOnlyList<SysMenu> IAdminContext.RoleMenus => RoleMenus;
    public string CookieName => "AdminBlazor_Auth";
    public ConcurrentBag<AdminMessageInfo> Messages { get; } = new();
    public ConcurrentDictionary<string, object?> Bags { get; } = new();

    public AdminContext(
        IServiceProvider service,
        FreeSqlCloud<string> fsqlCloud,
        IAdminSessionService sessionService,
        IAdminPermissionService permissionService,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        Service = service;
        _fsqlCloud = fsqlCloud;
        _sessionService = sessionService;
        _permissionService = permissionService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task InitAsync()
    {
        try
        {
            Tenant = await Orm.Select<SysTenant>().Where(a => a.Id == "main").FirstAsync();
        }
        catch { }

        var cookie = _httpContextAccessor?.HttpContext?.Request.Cookies[CookieName];
        var session = await _sessionService.LoadAsync(cookie);
        if (session != null)
        {
            Tenant = session.Tenant;
            User = session.User;
            Roles = session.Roles.ToList();
            RoleMenus = session.RoleMenus.ToList();
            _authorizedButtonPaths.UnionWith(session.ButtonPaths);
        }

        // No auto-login fallback — user must sign in via cookie or login form
        if (User == null)
        {
            Roles.Clear();
            RoleMenus.Clear();
            _authorizedButtonPaths.Clear();
        }
    }

    public async Task LoadRoleMenusAsync()
    {
        if (User == null)
        {
            RoleMenus.Clear();
            _authorizedButtonPaths.Clear();
            return;
        }

        RoleMenus = (await _permissionService.LoadAuthorizedMenusAsync(User.Id, Roles)).ToList();
        _authorizedButtonPaths.Clear();
        _authorizedButtonPaths.UnionWith(await _permissionService.LoadAuthorizedButtonPathsAsync(Roles));
    }

    public bool AuthPath(string path)
    {
        if (User == null) return false;
        if (Roles.Any(r => r.IsAdministrator)) return true;
        return FlattenMenus(RoleMenus).Any(m => string.Equals(m.Path, path, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<SysMenu> FlattenMenus(IEnumerable<SysMenu> menus)
    {
        foreach (var menu in menus)
        {
            yield return menu;
            if (menu.Children is not null)
            {
                foreach (var child in FlattenMenus(menu.Children)) yield return child;
            }
        }
    }

    /// <summary>
    /// 按钮权限检查 — 验证当前用户是否有指定按钮的权限
    /// </summary>
    public bool AuthButton(string buttonPath, string? buttonName = null)
    {
        if (User == null) return false;
        if (Roles.Any(r => r.IsAdministrator)) return true;

        return _authorizedButtonPaths.Contains(buttonPath);
    }

    public void SignIn(SysUser user)
    {
        User = user;
        Roles = user.Roles ?? new();
        _authorizedButtonPaths.Clear();
    }

    public async Task SignInAsync(SysUser user)
    {
        SignIn(user);
        await LoadRoleMenusAsync();
    }

    public void SignOut()
    {
        User = null;
        Roles.Clear();
        RoleMenus.Clear();
        _authorizedButtonPaths.Clear();
        var response = _httpContextAccessor?.HttpContext?.Response;
        if (response is { HasStarted: false })
        {
            response.Cookies.Delete(CookieName);
        }
    }

    public Task SendMessage(Guid receiveUserId, string content)
    {
        Messages.Add(new AdminMessageInfo { ReceiveUserId = receiveUserId, Content = content, Time = DateTime.Now });
        return Task.CompletedTask;
    }

    public void Dispose() { }
}
