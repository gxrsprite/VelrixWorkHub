using BootstrapBlazor.Components;
using FreeSql;
using VelrixWorkHub.Application.Platform;
using VelrixWorkHub.Infrastructure.Platform;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PlatformCatalogServiceTests
{
    [Fact]
    public async Task ParameterService_ValidatesSavesUpdatesAndDeletes()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"velrix-platform-catalog-{Guid.NewGuid():N}.db");
        using var fsql = new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, $"Data Source={databasePath}")
            .UseAutoSyncStructure(true)
            .Build();

        try
        {
            fsql.CodeFirst.SyncStructure<SysParam>();
            var service = new FreeSqlPlatformCatalogService(fsql);
            var actorId = Guid.CreateVersion7();
            var request = new PlatformParameterRequest("site.name", "站点名称", true, 1, "Velrix", null, null, null, null, null, null, "平台名称");

            var created = await service.SaveParameterAsync("site.name", request, actorId, "admin");
            Assert.True(created.Success);
            Assert.True(created.Created);
            Assert.Equal("Velrix", created.Value!.Value);
            Assert.Equal(actorId, fsql.Select<SysParam>().Where(item => item.Id == "site.name").First().CreatedUserId);

            var invalid = await service.SaveParameterAsync("site.name", request with { Id = "other" }, actorId, "admin");
            Assert.False(invalid.Success);
            Assert.Equal("路由参数编码与请求内容不一致", invalid.Error);

            var updated = await service.SaveParameterAsync("site.name", request with { Value = "Work Hub", Sort = 2 }, actorId, "admin");
            Assert.True(updated.Success);
            Assert.False(updated.Created);
            Assert.Equal("Work Hub", (await service.GetParameterAsync("site.name"))!.Value);

            var listed = await service.QueryParametersAsync("site", true, 20);
            Assert.Single(listed);
            Assert.True(await service.DeleteParameterAsync("site.name"));
            Assert.False(await service.DeleteParameterAsync("site.name"));
        }
        finally
        {
            fsql.Dispose();
            if (File.Exists(databasePath)) File.Delete(databasePath);
            if (File.Exists(databasePath + "-wal")) File.Delete(databasePath + "-wal");
            if (File.Exists(databasePath + "-shm")) File.Delete(databasePath + "-shm");
        }
    }

    [Fact]
    public async Task DictionaryService_ReturnsCategoriesItemsAndTree()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"velrix-platform-dict-{Guid.NewGuid():N}.db");
        using var fsql = new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, $"Data Source={databasePath}")
            .UseAutoSyncStructure(true)
            .Build();

        try
        {
            fsql.CodeFirst.SyncStructure<SysDict>();
            var categoryId = Guid.CreateVersion7();
            fsql.Insert(new SysDict { Id = categoryId, ParentId = Guid.Empty, Name = "颜色", Description = "颜色字典", Sort = 1 }).ExecuteAffrows();
            fsql.Insert(new SysDict { Id = Guid.CreateVersion7(), ParentId = categoryId, Name = "红色", Value = "red", Sort = 1 }).ExecuteAffrows();

            var service = new FreeSqlPlatformCatalogService(fsql);
            var categories = await service.QueryDictionaryCategoriesAsync(true);
            var items = await service.QueryDictionaryItemsAsync(null, "颜色", true);
            var tree = await service.QueryDictionaryTreeAsync(true);

            Assert.Single(categories);
            Assert.Null(items.Error);
            Assert.Single(items.Items!);
            Assert.Single(tree);
            Assert.Single(tree[0].Items);
        }
        finally
        {
            fsql.Dispose();
            if (File.Exists(databasePath)) File.Delete(databasePath);
            if (File.Exists(databasePath + "-wal")) File.Delete(databasePath + "-wal");
            if (File.Exists(databasePath + "-shm")) File.Delete(databasePath + "-shm");
        }
    }
}
