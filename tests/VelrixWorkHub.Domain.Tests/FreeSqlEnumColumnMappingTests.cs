using FreeSql;
using VelrixWorkHub.Infrastructure.Announcements;

namespace VelrixWorkHub.Domain.Tests;

public sealed class FreeSqlEnumColumnMappingTests
{
    [Fact]
    public void EnumPropertiesAreStoredAsNames()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"velrix-enum-{Guid.NewGuid():N}.db");
        using var fsql = new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, $"Data Source={databasePath}")
            .UseAutoSyncStructure(true)
            .Build();

        try
        {
            fsql.CodeFirst.SyncStructure<AnnouncementRecord>();
            var id = Guid.NewGuid();
            fsql.Insert(new AnnouncementRecord { Id = id, Title = "枚举映射", Content = "验证字符串存储", Status = VelrixWorkHub.Domain.AnnouncementStatus.Published }).ExecuteAffrows();

            var storedValue = fsql.Ado.QuerySingle<string>($"select Status from OaAnnouncement where Id = '{id}'");

            Assert.Equal("Published", storedValue);
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
