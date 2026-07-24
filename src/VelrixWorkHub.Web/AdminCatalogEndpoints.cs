using BootstrapBlazor.Components;
using FreeSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using AdminBlazor.Services;
using VelrixWorkHub.Application.Platform;

namespace AdminBlazor;

internal static class AdminCatalogEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/admin/params", async (HttpContext http, IFreeSql fsql, IAdminPermissionService authorization, IPlatformCatalogService catalog) =>
        {
            var (_, denied) = await AdminApiSupport.RequireMenuAccessAsync(http, fsql, authorization, "Admin/Param", "没有参数管理权限");
            if (denied != null)
                return denied;

            var search = http.Request.Query["search"].ToString();
            var enabledText = http.Request.Query["enabled"].ToString();
            var hasEnabled = bool.TryParse(enabledText, out var enabled);
            var take = AdminApiSupport.GetBoundedTake(http, 100, 500);

            var items = await catalog.QueryParametersAsync(search, hasEnabled ? enabled : null, take);
            return AdminApiSupport.ApiOk(items.Select(item => new AdminApiModels.ParamDto(
                    item.Id,
                    item.Title,
                    item.Enabled,
                    item.Sort,
                    item.Value,
                    item.Value2,
                    item.Description,
                    item.ModifiedTime)));
        });

        app.MapGet("/api/admin/params/{id}", async (string id, HttpContext http, IFreeSql fsql, IAdminPermissionService authorization, IPlatformCatalogService catalog) =>
        {
            var (_, denied) = await AdminApiSupport.RequireMenuAccessAsync(http, fsql, authorization, "Admin/Param", "没有参数管理权限");
            if (denied != null)
                return denied;

            var key = id.Trim();
            if (string.IsNullOrWhiteSpace(key))
                return AdminApiSupport.ApiError("参数编码不能为空", StatusCodes.Status400BadRequest);

            var item = await catalog.GetParameterAsync(key);
            return item == null
                ? AdminApiSupport.ApiError("参数不存在", StatusCodes.Status404NotFound)
                : AdminApiSupport.ApiOk(AdminApiSupport.ToParamDetailDto(item));
        });

        app.MapPut("/api/admin/params/{id}", async (string id, HttpContext http, IFreeSql fsql, IAdminPermissionService authorization, IPlatformCatalogService catalog) =>
        {
            var (user, denied) = await AdminApiSupport.RequireMenuAccessAsync(http, fsql, authorization, "Admin/Param", "没有参数管理权限");
            if (denied != null)
                return denied;

            var request = await http.Request.ReadFromJsonAsync<AdminApiModels.SaveParamRequest>();
            var save = await catalog.SaveParameterAsync(id, request == null ? null : new PlatformParameterRequest(
                request.Id,
                request.Title,
                request.Enabled,
                request.Sort,
                request.Value,
                request.Value2,
                request.Value3,
                request.Value4,
                request.Value5,
                request.Value6,
                request.Value7,
                request.Description), user!.Id, user.Username);
            if (!save.Success)
                return AdminApiSupport.ApiError(save.Error ?? "参数保存失败", StatusCodes.Status400BadRequest);

            return AdminApiSupport.ApiOk(AdminApiSupport.ToParamDetailDto(save.Value!), save.Created ? "参数已创建。" : "参数已保存。");
        });

        app.MapDelete("/api/admin/params/{id}", async (string id, HttpContext http, IFreeSql fsql, IAdminPermissionService authorization, IPlatformCatalogService catalog) =>
        {
            var (_, denied) = await AdminApiSupport.RequireMenuAccessAsync(http, fsql, authorization, "Admin/Param", "没有参数管理权限");
            if (denied != null)
                return denied;

            var key = id.Trim();
            if (string.IsNullOrWhiteSpace(key))
                return AdminApiSupport.ApiError("参数编码不能为空", StatusCodes.Status400BadRequest);

            return !await catalog.DeleteParameterAsync(key)
                ? AdminApiSupport.ApiError("参数不存在", StatusCodes.Status404NotFound)
                : AdminApiSupport.ApiOk(message: "参数已删除。");
        });

        app.MapGet("/api/admin/login-logs", async (HttpContext http, IFreeSql fsql, IAdminPermissionService permissions) =>
        {
            var (_, denied) = await AdminApiSupport.RequireAdministratorAsync(http, fsql, permissions, "只有管理员可以查看登录日志");
            if (denied != null)
                return denied;

            var username = http.Request.Query["username"].ToString();
            var typeText = http.Request.Query["type"].ToString();
            var hasType = Enum.TryParse<SysUserLoginLog.LogType>(typeText, ignoreCase: true, out var type);
            var take = AdminApiSupport.GetBoundedTake(http, 50, 500);

            var logs = await fsql.Select<SysUserLoginLog>()
                .WhereIf(!string.IsNullOrWhiteSpace(username), log => (log.Username ?? "").Contains(username))
                .WhereIf(hasType, log => log.Type == type)
                .OrderByDescending(log => log.LoginTime)
                .Take(take)
                .ToListAsync(log => new AdminApiModels.LoginLogDto(
                    log.Id,
                    log.Username,
                    log.Type.ToString(),
                    log.LoginTime,
                    log.Ip,
                    log.City,
                    log.OS,
                    log.Language,
                    log.UserAgent));

            return AdminApiSupport.ApiOk(logs);
        });

        app.MapGet("/api/admin/dicts/categories", async (HttpContext http, IFreeSql fsql, IAdminPermissionService authorization, IPlatformCatalogService catalog) =>
        {
            var (_, denied) = await AdminApiSupport.RequireMenuAccessAsync(http, fsql, authorization, "Admin/Dict", "没有字典管理权限");
            if (denied != null)
                return denied;

            var enabledText = http.Request.Query["enabled"].ToString();
            var hasEnabled = bool.TryParse(enabledText, out var enabled);
            var categories = await catalog.QueryDictionaryCategoriesAsync(hasEnabled ? enabled : null);
            return AdminApiSupport.ApiOk(categories.Select(item => new AdminApiModels.DictCategoryDto(
                    item.Id,
                    item.Name,
                    item.Description,
                    item.Enabled,
                    item.Sort)));
        });

        app.MapGet("/api/admin/dicts/items", async (HttpContext http, IFreeSql fsql, IAdminPermissionService authorization, IPlatformCatalogService catalog) =>
        {
            var (_, denied) = await AdminApiSupport.RequireMenuAccessAsync(http, fsql, authorization, "Admin/Dict", "没有字典管理权限");
            if (denied != null)
                return denied;

            var categoryIdText = http.Request.Query["categoryId"].ToString();
            var categoryName = http.Request.Query["categoryName"].ToString();
            var enabledText = http.Request.Query["enabled"].ToString();
            var hasEnabled = bool.TryParse(enabledText, out var enabled);

            var categoryId = Guid.Empty;
            if (!string.IsNullOrWhiteSpace(categoryIdText) && !Guid.TryParse(categoryIdText, out categoryId))
                return AdminApiSupport.ApiError("字典分类 ID 格式不正确", StatusCodes.Status400BadRequest);

            var result = await catalog.QueryDictionaryItemsAsync(categoryId == Guid.Empty ? null : categoryId, categoryName, hasEnabled ? enabled : null);
            if (result.Error != null)
                return AdminApiSupport.ApiError(result.Error, result.Error == "字典分类不存在" ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest);

            return AdminApiSupport.ApiOk(result.Items!.Select(item => new AdminApiModels.DictItemDto(
                    item.Id,
                    item.ParentId,
                    item.Name,
                    item.Value,
                    item.Value2,
                    item.Value3,
                    item.Value4,
                    item.Value5,
                    item.Description,
                    item.Enabled,
                    item.Sort)));
        });

        app.MapGet("/api/admin/dicts/tree", async (HttpContext http, IFreeSql fsql, IAdminPermissionService authorization, IPlatformCatalogService catalog) =>
        {
            var (_, denied) = await AdminApiSupport.RequireMenuAccessAsync(http, fsql, authorization, "Admin/Dict", "没有字典管理权限");
            if (denied != null)
                return denied;

            var enabledText = http.Request.Query["enabled"].ToString();
            var hasEnabled = bool.TryParse(enabledText, out var enabled);
            var tree = await catalog.QueryDictionaryTreeAsync(hasEnabled ? enabled : null);
            return AdminApiSupport.ApiOk(tree.Select(category => new AdminApiModels.DictTreeDto(
                    category.Id,
                    category.Name,
                    category.Description,
                    category.Enabled,
                    category.Sort,
                    category.Items.Select(item => new AdminApiModels.DictItemDto(
                            item.Id,
                            item.ParentId,
                            item.Name,
                            item.Value,
                            item.Value2,
                            item.Value3,
                            item.Value4,
                            item.Value5,
                            item.Description,
                            item.Enabled,
                            item.Sort))
                        .ToArray())));
        });
    }
}
