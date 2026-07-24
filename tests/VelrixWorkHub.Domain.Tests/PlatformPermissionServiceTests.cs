using BootstrapBlazor.Components;
using FreeSql;
using AdminBlazor.Services;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PlatformPermissionServiceTests
{
    [Fact]
    public async Task PermissionService_LoadsAncestorsAndButtonPathsForRegularRole()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"velrix-platform-permission-{Guid.NewGuid():N}.db");
        using var fsql = new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, $"Data Source={databasePath}")
            .UseAutoSyncStructure(true)
            .Build();

        try
        {
            fsql.CodeFirst.SyncStructure<SysRole>();
            fsql.CodeFirst.SyncStructure<SysMenu>();
            fsql.CodeFirst.SyncStructure<SysRoleUser>();
            fsql.CodeFirst.SyncStructure<SysRoleMenu>();

            var roleId = Guid.CreateVersion7();
            var userId = Guid.CreateVersion7();
            var rootId = Guid.CreateVersion7();
            var childId = Guid.CreateVersion7();
            var buttonId = Guid.CreateVersion7();
            fsql.Insert(new SysRole { Id = roleId, Name = "业务角色" }).ExecuteAffrows();
            fsql.Insert(new SysMenu { Id = rootId, ParentId = Guid.Empty, Label = "平台", Path = "Admin", Type = SysMenuType.菜单, Sort = 1 }).ExecuteAffrows();
            fsql.Insert(new SysMenu { Id = childId, ParentId = rootId, Label = "参数", Path = "Admin/Param", Type = SysMenuType.菜单, Sort = 2 }).ExecuteAffrows();
            fsql.Insert(new SysMenu { Id = buttonId, ParentId = childId, Label = "保存", Path = "Admin/Param/Save", Type = SysMenuType.按钮, Sort = 1 }).ExecuteAffrows();
            fsql.Insert(new SysRoleUser { RoleId = roleId, UserId = userId }).ExecuteAffrows();
            fsql.Insert(new SysRoleMenu { RoleId = roleId, MenuId = childId }).ExecuteAffrows();
            fsql.Insert(new SysRoleMenu { RoleId = roleId, MenuId = buttonId }).ExecuteAffrows();

            var service = new AdminAuthorizationService(fsql);
            var roles = await service.LoadUserRolesAsync(userId);
            var menus = await service.LoadAuthorizedMenusAsync(userId, roles);
            var buttons = await service.LoadAuthorizedButtonPathsAsync(roles);

            Assert.True(await service.CanAccessMenuAsync(userId, "admin/param"));
            Assert.False(await service.CanAccessMenuAsync(userId, "Admin/User"));
            Assert.Single(menus);
            Assert.Equal(rootId, menus[0].Id);
            Assert.Single(menus[0].Children);
            Assert.Equal(childId, menus[0].Children[0].Id);
            Assert.Equal(new[] { "Admin/Param/Save" }, buttons);
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
    public async Task PermissionService_AllowsAdministratorAcrossVisibleMenusAndButtons()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"velrix-platform-admin-permission-{Guid.NewGuid():N}.db");
        using var fsql = new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, $"Data Source={databasePath}")
            .UseAutoSyncStructure(true)
            .Build();

        try
        {
            fsql.CodeFirst.SyncStructure<SysRole>();
            fsql.CodeFirst.SyncStructure<SysMenu>();
            fsql.CodeFirst.SyncStructure<SysRoleUser>();
            fsql.CodeFirst.SyncStructure<SysRoleMenu>();

            var roleId = Guid.CreateVersion7();
            var userId = Guid.CreateVersion7();
            var menuId = Guid.CreateVersion7();
            var hiddenId = Guid.CreateVersion7();
            fsql.Insert(new SysRole { Id = roleId, Name = "管理员", IsAdministrator = true }).ExecuteAffrows();
            fsql.Insert(new SysMenu { Id = menuId, ParentId = Guid.Empty, Label = "用户", Path = "Admin/User", Type = SysMenuType.菜单 }).ExecuteAffrows();
            fsql.Insert(new SysMenu { Id = hiddenId, ParentId = Guid.Empty, Label = "隐藏", Path = "Admin/Hidden", Type = SysMenuType.按钮, IsHidden = true }).ExecuteAffrows();
            fsql.Insert(new SysRoleUser { RoleId = roleId, UserId = userId }).ExecuteAffrows();

            var service = new AdminAuthorizationService(fsql);
            Assert.True(await service.CanAccessMenuAsync(userId, "Admin/User"));
            var menus = await service.LoadAuthorizedMenusAsync(userId);
            var buttons = await service.LoadAuthorizedButtonPathsAsync(await service.LoadUserRolesAsync(userId));

            Assert.Single(menus);
            Assert.Equal(menuId, menus[0].Id);
            Assert.DoesNotContain("Admin/Hidden", buttons);
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
