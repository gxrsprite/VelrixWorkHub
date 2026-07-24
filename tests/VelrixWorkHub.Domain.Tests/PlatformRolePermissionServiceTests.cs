using BootstrapBlazor.Components;
using FreeSql;
using VelrixWorkHub.Infrastructure.Platform;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PlatformRolePermissionServiceTests
{
    [Fact]
    public async Task RolePermissionService_ReplacesMenuAndButtonAssignmentsIdempotently()
    {
        var databasePath = CreateDatabasePath();
        using var fsql = CreateDatabase(databasePath);

        try
        {
            SyncTables(fsql);
            var roleId = Guid.CreateVersion7();
            var menuId = Guid.CreateVersion7();
            var buttonId = Guid.CreateVersion7();
            fsql.Insert(new SysRole { Id = roleId, Name = "销售" }).ExecuteAffrows();
            fsql.Insert(new[]
            {
                new SysMenu { Id = menuId, Label = "客户", Path = "Crm/Customer", Type = SysMenuType.菜单 },
                new SysMenu { Id = buttonId, Label = "保存", Path = "Crm/Customer/Save", Type = SysMenuType.按钮 }
            }).ExecuteAffrows();

            var service = new FreeSqlAdminRolePermissionService(fsql);
            var actorId = Guid.CreateVersion7();
            var saved = await service.SaveRolePermissionsAsync(roleId, new[] { menuId, buttonId, menuId }, actorId, "admin");

            Assert.True(saved.Success);
            Assert.Equal(2, saved.AssignedCount);
            var snapshot = await service.LoadRolePermissionsAsync(roleId);
            Assert.NotNull(snapshot);
            Assert.Equal(new[] { menuId, buttonId }.OrderBy(id => id), snapshot!.MenuIds.OrderBy(id => id));
            var auditService = new FreeSqlAdminPermissionAuditService(fsql);
            var audits = await auditService.ListAsync(roleId, "RolePermission");
            Assert.Single(audits);
            Assert.Equal(actorId, audits[0].ActorUserId);
            Assert.Contains("menuIds", audits[0].AfterData);

            var replaced = await service.SaveRolePermissionsAsync(roleId, new[] { buttonId });
            Assert.True(replaced.Success);
            Assert.Equal(new[] { buttonId }, (await service.LoadRolePermissionsAsync(roleId))!.MenuIds);
            Assert.Equal(2, (await auditService.ListAsync(roleId, "RolePermission")).Count);
        }
        finally
        {
            fsql.Dispose();
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task RolePermissionService_RejectsUnknownMenuWithoutChangingExistingAssignments()
    {
        var databasePath = CreateDatabasePath();
        using var fsql = CreateDatabase(databasePath);

        try
        {
            SyncTables(fsql);
            var roleId = Guid.CreateVersion7();
            var menuId = Guid.CreateVersion7();
            fsql.Insert(new SysRole { Id = roleId, Name = "销售" }).ExecuteAffrows();
            fsql.Insert(new SysMenu { Id = menuId, Label = "客户", Path = "Crm/Customer", Type = SysMenuType.菜单 }).ExecuteAffrows();

            var service = new FreeSqlAdminRolePermissionService(fsql);
            Assert.True((await service.SaveRolePermissionsAsync(roleId, new[] { menuId })).Success);

            var unknownId = Guid.CreateVersion7();
            var rejected = await service.SaveRolePermissionsAsync(roleId, new[] { unknownId });

            Assert.False(rejected.Success);
            Assert.Contains("不存在", rejected.Error);
            Assert.Equal(new[] { menuId }, (await service.LoadRolePermissionsAsync(roleId))!.MenuIds);
            Assert.Single(await new FreeSqlAdminPermissionAuditService(fsql).ListAsync(roleId, "RolePermission"));
        }
        finally
        {
            fsql.Dispose();
            DeleteDatabase(databasePath);
        }
    }

    private static IFreeSql CreateDatabase(string databasePath) => new FreeSqlBuilder()
        .UseConnectionString(DataType.Sqlite, $"Data Source={databasePath}")
        .UseAutoSyncStructure(true)
        .Build();

    private static void SyncTables(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<SysRole>();
        fsql.CodeFirst.SyncStructure<SysMenu>();
        fsql.CodeFirst.SyncStructure<SysRoleMenu>();
        fsql.CodeFirst.SyncStructure<SysPermissionAuditLog>();
    }

    private static string CreateDatabasePath() => Path.Combine(
        Path.GetTempPath(), $"velrix-platform-role-permission-{Guid.NewGuid():N}.db");

    private static void DeleteDatabase(string databasePath)
    {
        if (File.Exists(databasePath)) File.Delete(databasePath);
        if (File.Exists(databasePath + "-wal")) File.Delete(databasePath + "-wal");
        if (File.Exists(databasePath + "-shm")) File.Delete(databasePath + "-shm");
    }
}
