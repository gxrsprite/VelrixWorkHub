using BootstrapBlazor.Components;
using FreeSql;
using VelrixWorkHub.Application.Platform;

namespace VelrixWorkHub.Infrastructure.Platform;

public sealed class FreeSqlPlatformCatalogService(IFreeSql fsql) : IPlatformCatalogService
{
    public async Task<IReadOnlyList<PlatformParameterSummary>> QueryParametersAsync(string? search, bool? enabled, int take)
    {
        return await fsql.Select<SysParam>()
            .WhereIf(!string.IsNullOrWhiteSpace(search), item =>
                (item.Id ?? "").Contains(search!) || (item.Title ?? "").Contains(search!) || (item.Description ?? "").Contains(search!))
            .WhereIf(enabled.HasValue, item => item.Enabled == enabled!.Value)
            .OrderBy(item => item.Sort)
            .OrderBy(item => item.Id)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(item => new PlatformParameterSummary(
                item.Id,
                item.Title,
                item.Enabled,
                item.Sort,
                item.Value,
                item.Value2,
                item.Description,
                item.ModifiedTime));
    }

    public async Task<PlatformParameterDetail?> GetParameterAsync(string id)
    {
        var item = await fsql.Select<SysParam>().Where(param => param.Id == id.Trim()).FirstAsync();
        return item == null ? null : ToDetail(item);
    }

    public async Task<PlatformParameterSaveResult> SaveParameterAsync(
        string routeId,
        PlatformParameterRequest? request,
        Guid actorId,
        string? actorName)
    {
        var error = ValidateRequest(routeId, request, out var key);
        if (error != null)
            return new PlatformParameterSaveResult(false, false, error, null);

        var found = await fsql.Select<SysParam>().Where(param => param.Id == key).FirstAsync();
        var created = found == null;
        SysParam existing;
        if (created)
        {
            existing = new SysParam
            {
                Id = key,
                CreatedUserId = actorId,
                CreatedUserName = actorName ?? string.Empty,
                CreatedTime = DateTime.Now
            };
        }
        else
        {
            existing = found!;
        }

        ApplyRequest(existing, request!);
        if (created)
        {
            await fsql.Insert(existing).ExecuteAffrowsAsync();
        }
        else
        {
            existing.ModifiedUserId = actorId;
            existing.ModifiedUserName = actorName ?? string.Empty;
            existing.ModifiedTime = DateTime.Now;
            await fsql.Update<SysParam>().SetSource(existing).ExecuteAffrowsAsync();
        }

        return new PlatformParameterSaveResult(true, created, null, ToDetail(existing));
    }

    public async Task<bool> DeleteParameterAsync(string id)
    {
        return await fsql.Delete<SysParam>()
            .Where(param => param.Id == id.Trim())
            .ExecuteAffrowsAsync() > 0;
    }

    public async Task<IReadOnlyList<PlatformDictionaryCategory>> QueryDictionaryCategoriesAsync(bool? enabled)
    {
        return await fsql.Select<SysDict>()
            .Where(item => item.ParentId == Guid.Empty)
            .WhereIf(enabled.HasValue, item => item.Enabled == enabled!.Value)
            .OrderBy(item => item.Sort)
            .OrderBy(item => item.Name)
            .ToListAsync(item => new PlatformDictionaryCategory(item.Id, item.Name, item.Description, item.Enabled, item.Sort));
    }

    public async Task<PlatformDictionaryItemsResult> QueryDictionaryItemsAsync(
        Guid? categoryId,
        string? categoryName,
        bool? enabled)
    {
        var resolvedCategoryId = categoryId.GetValueOrDefault();
        if (resolvedCategoryId == Guid.Empty && !string.IsNullOrWhiteSpace(categoryName))
        {
            var category = await fsql.Select<SysDict>()
                .Where(item => item.ParentId == Guid.Empty && item.Name == categoryName)
                .FirstAsync();
            if (category == null)
            {
                return new PlatformDictionaryItemsResult(null, "字典分类不存在");
            }
            resolvedCategoryId = category.Id;
        }

        if (resolvedCategoryId == Guid.Empty)
        {
            return new PlatformDictionaryItemsResult(null, "请提供 categoryId 或 categoryName");
        }

        var entities = await fsql.Select<SysDict>()
            .Where(item => item.ParentId == resolvedCategoryId)
            .WhereIf(enabled.HasValue, item => item.Enabled == enabled!.Value)
            .OrderBy(item => item.Sort)
            .OrderBy(item => item.Name)
            .ToListAsync();
        return new PlatformDictionaryItemsResult(entities.Select(ToItem).ToArray(), null);
    }

    public async Task<IReadOnlyList<PlatformDictionaryTree>> QueryDictionaryTreeAsync(bool? enabled)
    {
        var all = await fsql.Select<SysDict>()
            .WhereIf(enabled.HasValue, item => item.Enabled == enabled!.Value)
            .OrderBy(item => item.Sort)
            .OrderBy(item => item.Name)
            .ToListAsync();
        var itemsByParent = all.Where(item => item.ParentId != Guid.Empty).ToLookup(item => item.ParentId);
        return all.Where(item => item.ParentId == Guid.Empty)
            .Select(category => new PlatformDictionaryTree(
                category.Id,
                category.Name,
                category.Description,
                category.Enabled,
                category.Sort,
                itemsByParent[category.Id].OrderBy(item => item.Sort).ThenBy(item => item.Name).Select(ToItem).ToArray()))
            .ToArray();
    }

    private static string? ValidateRequest(string routeId, PlatformParameterRequest? request, out string key)
    {
        key = routeId.Trim();
        if (request == null) return "请求内容不能为空";
        if (string.IsNullOrWhiteSpace(key)) return "参数编码不能为空";
        if (key.Length > 50) return "参数编码不能超过 50 个字符";
        if (!string.IsNullOrWhiteSpace(request.Id) && !string.Equals(key, request.Id.Trim(), StringComparison.Ordinal)) return "路由参数编码与请求内容不一致";
        if (string.IsNullOrWhiteSpace(request.Title)) return "参数标题不能为空";
        if (request.Title.Trim().Length > 500) return "参数标题不能超过 500 个字符";
        if (request.Description?.Length > 500) return "参数描述不能超过 500 个字符";
        if (new[] { request.Value, request.Value2, request.Value3, request.Value4, request.Value5, request.Value6, request.Value7 }.Any(value => value?.Length > 1024)) return "参数值不能超过 1024 个字符";
        return null;
    }

    private static void ApplyRequest(SysParam item, PlatformParameterRequest request)
    {
        item.Title = request.Title?.Trim() ?? string.Empty;
        item.Enabled = request.Enabled ?? true;
        item.Sort = request.Sort;
        item.Value = request.Value ?? string.Empty;
        item.Value2 = request.Value2 ?? string.Empty;
        item.Value3 = request.Value3 ?? string.Empty;
        item.Value4 = request.Value4 ?? string.Empty;
        item.Value5 = request.Value5 ?? string.Empty;
        item.Value6 = request.Value6 ?? string.Empty;
        item.Value7 = request.Value7 ?? string.Empty;
        item.Description = request.Description ?? string.Empty;
    }

    private static PlatformParameterDetail ToDetail(SysParam item) => new(
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

    private static PlatformDictionaryItem ToItem(SysDict item) => new(
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
        item.Sort);
}
