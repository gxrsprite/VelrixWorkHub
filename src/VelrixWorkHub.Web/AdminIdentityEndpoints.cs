using BootstrapBlazor.Components;
using FreeSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using AdminBlazor.Services;
using VelrixWorkHub.Application.Platform;

namespace AdminBlazor;

internal static class AdminIdentityEndpoints
{
    public static void Map(WebApplication app, AdminBlazorOptions? adminOptions, long maxUploadBytes)
    {
        app.MapPost("/api/admin/login", async (HttpContext http, IFreeSql fsql, AdminAuthCookieService authCookie, LoginAttemptLimiter loginLimiter) =>
        {
            var form = await http.Request.ReadFromJsonAsync<AdminApiModels.LoginRequest>();
            if (form == null || string.IsNullOrWhiteSpace(form.Username) || string.IsNullOrWhiteSpace(form.Password))
                return AdminApiSupport.ApiError("请输入用户名和密码");

            var loginKey = $"{http.Connection.RemoteIpAddress}|{form.Username.Trim().ToUpperInvariant()}";
            if (loginLimiter.IsBlocked(loginKey, out var retryAfter))
                return AdminApiSupport.ApiError($"登录尝试过多，请在 {(int)Math.Ceiling(retryAfter.TotalMinutes)} 分钟后重试。", StatusCodes.Status429TooManyRequests);

            var user = await fsql.Select<SysUser>()
                .IncludeMany(a => a.Roles)
                .Where(a => a.Username == form.Username)
                .FirstAsync();

            if (user == null || !PasswordHasher.Verify(form.Password, user.PasswordHash, user.Password))
            {
                var blocked = loginLimiter.RegisterFailure(loginKey, out retryAfter);
                await fsql.Insert(new SysUserLoginLog
                {
                    Id = Guid.CreateVersion7(),
                    Username = form.Username,
                    Type = SysUserLoginLog.LogType.登陆失败,
                    LoginTime = DateTime.Now
                }).ExecuteAffrowsAsync();
                return AdminApiSupport.ApiError(
                    blocked ? $"登录尝试过多，请在 {(int)Math.Ceiling(retryAfter.TotalMinutes)} 分钟后重试。" : "用户名或密码错误",
                    blocked ? StatusCodes.Status429TooManyRequests : StatusCodes.Status401Unauthorized);
            }

            if (!user.IsEnabled)
                return AdminApiSupport.ApiError("账号已被禁用");

            loginLimiter.Reset(loginKey);

            if (PasswordHasher.RequiresFormatUpgrade(user.PasswordHash))
            {
                user.PasswordHash = PasswordHasher.Hash(form.Password);
                user.Password = null;
                await fsql.Update<SysUser>()
                    .Set(a => a.PasswordHash, user.PasswordHash)
                    .Set(a => a.Password, (string?)null)
                    .Where(a => a.Id == user.Id)
                    .ExecuteAffrowsAsync();
            }

            await fsql.Insert(new SysUserLoginLog
            {
                Id = Guid.CreateVersion7(),
                Username = user.Username,
                Type = SysUserLoginLog.LogType.登陆成功,
                LoginTime = DateTime.Now
            }).ExecuteAffrowsAsync();

            await fsql.Update<SysUser>().Set(a => a.LoginTime, DateTime.Now).Where(a => a.Id == user.Id).ExecuteAffrowsAsync();

            var authExpiresAt = DateTimeOffset.UtcNow.Add(form.Remember ? TimeSpan.FromDays(15) : TimeSpan.FromHours(12));
            http.Response.Cookies.Append("AdminBlazor_Auth", authCookie.Protect(user.Id, user.AuthVersion, authExpiresAt), new CookieOptions
            {
                HttpOnly = true,
                Secure = http.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                IsEssential = true,
                Expires = form.Remember ? authExpiresAt : null
            });

            return AdminApiSupport.ApiOk();
        });

        app.MapGet("/api/admin/logout", (HttpContext http) =>
        {
            http.Response.Cookies.Delete("AdminBlazor_Auth", new CookieOptions { Path = "/" });
            return Results.Redirect("/Login");
        });

        app.MapGet("/api/admin/profile", async (HttpContext http, IFreeSql fsql, IAdminPermissionService permissions) =>
        {
            var (user, denied) = await AdminApiSupport.RequireUserAsync(http, fsql);
            if (denied != null)
                return denied;

            var roles = (await permissions.LoadUserRolesAsync(user!.Id))
                .Select(role => new AdminApiModels.ProfileRoleDto(role.Id, role.Name, role.IsAdministrator))
                .ToArray();

            return AdminApiSupport.ApiOk(new AdminApiModels.ProfileDto(user!.Id, user.Username, user.Nickname, user.IsEnabled, user.LoginTime, roles));
        });

        app.MapGet("/api/admin/session", async (HttpContext http, IFreeSql fsql, IAdminPermissionService permissions) =>
        {
            var (user, denied) = await AdminApiSupport.RequireUserAsync(http, fsql);
            if (denied != null)
                return denied;

            var roles = await permissions.LoadUserRolesAsync(user!.Id);
            var menus = await permissions.LoadAuthorizedMenusAsync(user.Id, roles);
            var buttonPaths = await permissions.LoadAuthorizedButtonPathsAsync(roles);

            return AdminApiSupport.ApiOk(new AdminApiModels.SessionDto(
                user.Id,
                user.Username,
                user.Nickname,
                user.IsEnabled,
                user.LoginTime,
                roles.Select(role => new AdminApiModels.ProfileRoleDto(role.Id, role.Name, role.IsAdministrator)).ToArray(),
                AdminApiSupport.FlattenMenus(menus).Select(menu => menu.Path).Where(path => !string.IsNullOrWhiteSpace(path)).Distinct().ToArray(),
                buttonPaths));
        });

        app.MapGet("/api/admin/menus", async (HttpContext http, IFreeSql fsql, IAdminPermissionService permissions) =>
        {
            var (user, denied) = await AdminApiSupport.RequireUserAsync(http, fsql);
            if (denied != null)
                return denied;

            var roles = await permissions.LoadUserRolesAsync(user!.Id);
            var menus = await permissions.LoadAuthorizedMenusAsync(user.Id, roles);
            return AdminApiSupport.ApiOk(AdminApiSupport.BuildMenuDtos(menus));
        });

        app.MapGet("/api/admin/runtime-config", async (
            HttpContext http,
            IFreeSql fsql,
            PasswordPolicyOptions passwordPolicyOptions,
            LoginAttemptLimiterOptions loginAttemptLimiterOptions) =>
        {
            var (_, denied) = await AdminApiSupport.RequireUserAsync(http, fsql);
            if (denied != null)
                return denied;

            return AdminApiSupport.ApiOk(new AdminApiModels.RuntimeConfigDto(
                adminOptions?.AutoSyncStructure ?? true,
                maxUploadBytes,
                FileSize.Format(maxUploadBytes),
                new AdminApiModels.PasswordPolicyDto(
                    passwordPolicyOptions.MinimumLength,
                    passwordPolicyOptions.MaximumLength,
                    passwordPolicyOptions.RequireUppercase,
                    passwordPolicyOptions.RequireLowercase,
                    passwordPolicyOptions.RequireDigit),
                new AdminApiModels.LoginAttemptLimiterDto(
                    loginAttemptLimiterOptions.MaxFailures,
                    (int)loginAttemptLimiterOptions.FailureWindow.TotalMinutes,
                    (int)loginAttemptLimiterOptions.BlockDuration.TotalMinutes)));
        });
    }
}
