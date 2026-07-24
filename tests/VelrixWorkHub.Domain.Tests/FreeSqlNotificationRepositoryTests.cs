using FreeSql;
using VelrixWorkHub.Domain;
using VelrixWorkHub.Infrastructure.Notifications;

namespace VelrixWorkHub.Domain.Tests;

public sealed class FreeSqlNotificationRepositoryTests
{
    [Fact]
    public void Add_UsesRecipientAndDedupeKeyAsDatabaseUniqueBoundary()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"velrix-notification-{Guid.NewGuid():N}.db");
        try
        {
            using var fsql = new FreeSqlBuilder()
                .UseConnectionString(DataType.Sqlite, $"Data Source={databasePath}")
                .UseAutoSyncStructure(true)
                .Build();
            var repository = new FreeSqlNotificationRepository(fsql);
            var first = new WorkNotification("ADMIN", WorkNotificationKind.Approval, "待审批", "请处理", "/Workflow/Inbox", "workflow-task:1");
            repository.Add(first);

            var duplicate = new WorkNotification("admin", WorkNotificationKind.Approval, "重复待审批", "请再次处理", "/Workflow/Inbox", "workflow-task:1");

            Assert.ThrowsAny<Exception>(() => repository.Add(duplicate));
            Assert.Single(repository.List("admin"));
        }
        finally
        {
            if (File.Exists(databasePath)) File.Delete(databasePath);
            if (File.Exists(databasePath + "-wal")) File.Delete(databasePath + "-wal");
            if (File.Exists(databasePath + "-shm")) File.Delete(databasePath + "-shm");
        }
    }
}
