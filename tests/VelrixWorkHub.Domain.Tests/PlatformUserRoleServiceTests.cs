using BootstrapBlazor.Components;
using FreeSql;
using VelrixWorkHub.Infrastructure.Platform;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PlatformUserRoleServiceTests
{
    [Fact]
    public async Task UserRoleService_ReplacesAssignmentsAndDeduplicatesRoleIds()
    {
        var databasePath = CreateDatabasePath();
        using var fsql = CreateDatabase(databasePath);

        try
        {
            SyncTables(fsql);
            var userId = Guid.CreateVersion7();
            var roleId = Guid.CreateVersion7();
            var secondRoleId = Guid.CreateVersion7();
            fsql.Insert(new SysUser { Id = userId, Username = "sales", IsEnabled = true }).ExecuteAffrows();
            fsql.Insert(new[]
            {
                new SysRole { Id = roleId, Name = "销售" },
                new SysRole { Id = secondRoleId, Name = "跟进" }
            }).ExecuteAffrows();

            var service = new FreeSqlAdminUserRoleService(fsql);
            var saved = await service.SaveUserRolesAsync(userId, new[] { roleId, secondRoleId, roleId });

            Assert.True(saved.Success);
            Assert.Equal(2, saved.AssignedCount);
            Assert.Equal(new[] { roleId, secondRoleId }.OrderBy(id => id),
                (await service.LoadUserRoleIdsAsync(userId)).OrderBy(id => id));

            var replaced = await service.SaveUserRolesAsync(userId, new[] { secondRoleId });
            Assert.True(replaced.Success);
            Assert.Equal(new[] { secondRoleId }, await service.LoadUserRoleIdsAsync(userId));
        }
        finally
        {
            fsql.Dispose();
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task UserRoleService_RejectsUnknownRoleWithoutChangingAssignments()
    {
        var databasePath = CreateDatabasePath();
        using var fsql = CreateDatabase(databasePath);

        try
        {
            SyncTables(fsql);
            var userId = Guid.CreateVersion7();
            var roleId = Guid.CreateVersion7();
            fsql.Insert(new SysUser { Id = userId, Username = "sales", IsEnabled = true }).ExecuteAffrows();
            fsql.Insert(new SysRole { Id = roleId, Name = "销售" }).ExecuteAffrows();

            var service = new FreeSqlAdminUserRoleService(fsql);
            Assert.True((await service.SaveUserRolesAsync(userId, new[] { roleId })).Success);

            var rejected = await service.SaveUserRolesAsync(userId, new[] { Guid.CreateVersion7() });

            Assert.False(rejected.Success);
            Assert.Contains("不存在", rejected.Error);
            Assert.Equal(new[] { roleId }, await service.LoadUserRoleIdsAsync(userId));
            Assert.Single(await new FreeSqlAdminPermissionAuditService(fsql).ListAsync(userId, "UserRole"));
        }
        finally
        {
            fsql.Dispose();
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task UserRoleService_ProtectsTheLastAdministrator()
    {
        var databasePath = CreateDatabasePath();
        using var fsql = CreateDatabase(databasePath);

        try
        {
            SyncTables(fsql);
            var userId = Guid.CreateVersion7();
            var administratorRoleId = Guid.CreateVersion7();
            var normalRoleId = Guid.CreateVersion7();
            fsql.Insert(new SysUser { Id = userId, Username = "admin", IsEnabled = true }).ExecuteAffrows();
            fsql.Insert(new[]
            {
                new SysRole { Id = administratorRoleId, Name = "管理员", IsAdministrator = true },
                new SysRole { Id = normalRoleId, Name = "普通用户" }
            }).ExecuteAffrows();

            var service = new FreeSqlAdminUserRoleService(fsql);
            Assert.True((await service.SaveUserRolesAsync(userId, new[] { administratorRoleId })).Success);

            var rejected = await service.SaveUserRolesAsync(userId, new[] { normalRoleId });

            Assert.False(rejected.Success);
            Assert.Contains("至少保留一名管理员", rejected.Error);
            Assert.Equal(new[] { administratorRoleId }, await service.LoadUserRoleIdsAsync(userId));
            Assert.Single(await new FreeSqlAdminPermissionAuditService(fsql).ListAsync(userId, "UserRole"));
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
        fsql.CodeFirst.SyncStructure<SysUser>();
        fsql.CodeFirst.SyncStructure<SysRole>();
        fsql.CodeFirst.SyncStructure<SysRoleUser>();
        fsql.CodeFirst.SyncStructure<SysPermissionAuditLog>();
    }

    private static string CreateDatabasePath() => Path.Combine(
        Path.GetTempPath(), $"velrix-platform-user-role-{Guid.NewGuid():N}.db");

    private static void DeleteDatabase(string databasePath)
    {
        if (File.Exists(databasePath)) File.Delete(databasePath);
        if (File.Exists(databasePath + "-wal")) File.Delete(databasePath + "-wal");
        if (File.Exists(databasePath + "-shm")) File.Delete(databasePath + "-shm");
    }
}
