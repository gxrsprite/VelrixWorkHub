using FreeSql;
using VelrixWorkHub.Domain;
using VelrixWorkHub.Infrastructure.Tasks;

namespace VelrixWorkHub.Domain.Tests;

public sealed class FreeSqlWorkTaskRepositoryTests
{
    [Fact]
    public void Crud_RoundTripsTaskStateThroughFreeSql()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"velrix-work-task-{Guid.NewGuid():N}.db");
        try
        {
            using var fsql = new FreeSqlBuilder()
                .UseConnectionString(DataType.Sqlite, $"Data Source={databasePath}")
                .UseAutoSyncStructure(true)
                .Build();
            var repository = new FreeSqlWorkTaskRepository(fsql);
            var task = new WorkTask("数据库任务", "初始备注", DateOnly.FromDateTime(DateTime.Today.AddDays(1)));

            repository.Add(task);
            var loaded = Assert.Single(repository.List());
            Assert.Equal(task.Id, loaded.Id);
            Assert.Equal(task.Title, loaded.Title);

            loaded.Start();
            repository.Update(loaded);
            var updated = Assert.Single(repository.List());
            Assert.Equal(WorkTaskStatus.InProgress, updated.Status);

            repository.Remove(updated.Id);
            Assert.Empty(repository.List());
        }
        finally
        {
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }
}
