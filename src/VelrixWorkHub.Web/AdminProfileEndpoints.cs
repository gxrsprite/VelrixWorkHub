using FreeSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace AdminBlazor;

internal static class AdminProfileEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPut("/api/admin/profile", async (HttpContext http, IFreeSql fsql) =>
        {
            var (user, denied) = await AdminApiSupport.RequireUserAsync(http, fsql);
            if (denied != null)
                return denied;

            var request = await http.Request.ReadFromJsonAsync<AdminApiModels.UpdateProfileRequest>();
            var nickname = request?.Nickname?.Trim();
            if (string.IsNullOrWhiteSpace(nickname))
                return AdminApiSupport.ApiError("昵称不能为空", StatusCodes.Status400BadRequest);
            if (nickname.Length > 50)
                return AdminApiSupport.ApiError("昵称不能超过 50 个字符", StatusCodes.Status400BadRequest);

            await fsql.Update<SysUser>()
                .Set(item => item.Nickname, nickname)
                .Where(item => item.Id == user!.Id)
                .ExecuteAffrowsAsync();

            return AdminApiSupport.ApiOk(new { nickname }, "个人资料已保存。");
        });

        app.MapPost("/api/admin/profile/password", async (HttpContext http, IFreeSql fsql, PasswordPolicy passwordPolicy) =>
        {
            var (user, denied) = await AdminApiSupport.RequireUserAsync(http, fsql);
            if (denied != null)
                return denied;

            var request = await http.Request.ReadFromJsonAsync<AdminApiModels.ChangePasswordRequest>();
            if (request == null || string.IsNullOrWhiteSpace(request.OldPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
                return AdminApiSupport.ApiError("请输入旧密码和新密码", StatusCodes.Status400BadRequest);
            if (request.NewPassword != request.ConfirmPassword)
                return AdminApiSupport.ApiError("两次输入的新密码不一致", StatusCodes.Status400BadRequest);

            var passwordError = passwordPolicy.Validate(request.NewPassword);
            if (passwordError != null)
                return AdminApiSupport.ApiError(passwordError, StatusCodes.Status400BadRequest);
            if (!PasswordHasher.Verify(request.OldPassword, user!.PasswordHash, user.Password))
                return AdminApiSupport.ApiError("旧密码不正确", StatusCodes.Status400BadRequest);

            await fsql.Update<SysUser>()
                .Set(item => item.PasswordHash, PasswordHasher.Hash(request.NewPassword))
                .Set(item => item.Password, (string?)null)
                .Set(item => item.AuthVersion, user!.AuthVersion + 1)
                .Where(item => item.Id == user!.Id)
                .ExecuteAffrowsAsync();
            http.Response.Cookies.Delete("AdminBlazor_Auth", new CookieOptions { Path = "/" });
            return AdminApiSupport.ApiOk(message: "密码已修改，请重新登录。");
        });

        app.MapPost("/api/admin/logout-all", async (HttpContext http, IFreeSql fsql) =>
        {
            var (user, denied) = await AdminApiSupport.RequireUserAsync(http, fsql);
            if (denied != null)
                return denied;

            await fsql.Update<SysUser>()
                .Set(item => item.AuthVersion, user!.AuthVersion + 1)
                .Where(item => item.Id == user!.Id)
                .ExecuteAffrowsAsync();
            http.Response.Cookies.Delete("AdminBlazor_Auth", new CookieOptions { Path = "/" });
            return AdminApiSupport.ApiOk(message: "所有设备已退出，请重新登录。");
        });
    }
}

