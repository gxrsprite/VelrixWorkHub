using AdminBlazor.Services;
using BootstrapBlazor.Components;
using FreeSql;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using VelrixWorkHub.Application.Platform;
using VelrixWorkHub.Infrastructure.Platform;

namespace AdminBlazor;

internal static class AdminApiSupport
{
    internal static async Task<(SysUser? User, IResult? Error)> RequireMenuAccessAsync(
        HttpContext http,
        IFreeSql fsql,
        IAdminPermissionService authorization,
        string path,
        string forbiddenMessage)
    {
        var (user, error) = await RequireUserAsync(http, fsql);
        if (error != null || user == null)
            return (null, error);

        return await authorization.CanAccessMenuAsync(user.Id, path)
            ? (user, null)
            : (null, ApiError(forbiddenMessage, StatusCodes.Status403Forbidden));
    }

    internal static async Task<(SysUser? User, IResult? Error)> RequireButtonAccessAsync(
        HttpContext http,
        IFreeSql fsql,
        IAdminPermissionService permissions,
        string menuPath,
        string buttonPath,
        string forbiddenMessage)
    {
        var (user, denied) = await RequireMenuAccessAsync(http, fsql, permissions, menuPath, forbiddenMessage);
        if (denied != null || user is null) return (user, denied);
        var roles = await permissions.LoadUserRolesAsync(user.Id);
        var buttons = await permissions.LoadAuthorizedButtonPathsAsync(roles);
        return buttons.Contains(buttonPath, StringComparer.OrdinalIgnoreCase)
            ? (user, null)
            : (user, ApiError(forbiddenMessage, StatusCodes.Status403Forbidden));
    }

    internal static async Task<(SysUser? User, IResult? Error)> RequireAdministratorAsync(HttpContext http, IFreeSql fsql, IAdminPermissionService permissions, string forbiddenMessage)
    {
        var (user, error) = await RequireUserAsync(http, fsql);
        if (error != null || user == null)
            return (null, error);

        var roles = await permissions.LoadUserRolesAsync(user.Id);
        return roles.Any(role => role.IsAdministrator)
            ? (user, null)
            : (null, ApiError(forbiddenMessage, StatusCodes.Status403Forbidden));
    }

    internal static async Task<(SysUser? User, IResult? Error)> RequireUserAsync(HttpContext http, IFreeSql fsql)
    {
        var user = await ResolveUserAsync(http, fsql);
        return user == null
            ? (null, ApiError("未登录或登录失效", StatusCodes.Status401Unauthorized, 8888))
            : (user, null);
    }

    private static async Task<SysUser?> ResolveUserAsync(HttpContext http, IFreeSql fsql)
    {
        var cookie = http.Request.Cookies["AdminBlazor_Auth"];
        var authCookie = http.RequestServices.GetRequiredService<AdminAuthCookieService>();
        if (!authCookie.TryGetSession(cookie, out var session))
            return null;

        var user = await fsql.Select<SysUser>()
            .Where(a => a.Id == session.UserId && a.IsEnabled)
            .FirstAsync();
        return user?.AuthVersion == session.AuthVersion ? user : null;
    }

    internal static IReadOnlyList<AdminApiModels.MenuDto> BuildMenuDtos(IReadOnlyList<SysMenu> menus)
    {
        return menus.OrderBy(menu => menu.Sort).Select(menu => new AdminApiModels.MenuDto(
            menu.Id, menu.Label, menu.Icon, menu.Path, menu.Sort, BuildMenuDtos(menu.Children ?? []))).ToArray();
    }

    internal static IEnumerable<SysMenu> FlattenMenus(IEnumerable<SysMenu> menus)
    {
        foreach (var menu in menus)
        {
            yield return menu;
            foreach (var child in FlattenMenus(menu.Children ?? []))
                yield return child;
        }
    }

    internal static int GetBoundedTake(HttpContext http, int defaultValue, int maxValue)
    {
        return int.TryParse(http.Request.Query["take"], out var parsedTake)
            ? Math.Clamp(parsedTake, 1, maxValue)
            : defaultValue;
    }

    internal static AdminApiModels.ParamDetailDto ToParamDetailDto(PlatformParameterDetail item) => new(
        item.Id,
        item.Title,
        item.Enabled,
        item.Sort,
        item.Value,
        item.Value2,
        item.Value3,
        item.Value4,
        item.Value5,
        item.Value6,
        item.Value7,
        item.Description,
        item.CreatedTime,
        item.ModifiedTime);

    internal static IResult ApiOk(object? data = null, string? message = null) =>
        Results.Json(new AdminApiModels.AdminApiResponse(true, null, null, data, message));

    internal static IResult ApiError(string error, int statusCode = StatusCodes.Status200OK, int? code = null) =>
        Results.Json(new AdminApiModels.AdminApiResponse(false, code, error, null, null), statusCode: statusCode);
}
