using FreeSql;
using VelrixWorkHub.Application.Schedules;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Schedules;
public sealed class FreeSqlWorkScheduleRepository(IFreeSql fsql) : IWorkScheduleRepository
{
    public IReadOnlyList<WorkSchedule> List() => fsql.Select<WorkScheduleRecord>().OrderBy(item => item.StartTime).ToList().Select(ToDomain).ToArray();
    public void Add(WorkSchedule item) { var now = DateTime.Now; fsql.Insert(ToRecord(item, now, now)).ExecuteAffrows(); }
    public void Update(WorkSchedule item)
    {
        var rows = fsql.Update<WorkScheduleRecord>().Set(record => record.Title, item.Title).Set(record => record.Description, item.Description).Set(record => record.Location, item.Location).Set(record => record.StartTime, item.StartTime).Set(record => record.EndTime, item.EndTime).Set(record => record.ModifiedTime, DateTime.Now).Where(record => record.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("日程不存在或已被删除。");
    }
    public void Remove(Guid id) => fsql.Delete<WorkScheduleRecord>().Where(item => item.Id == id).ExecuteAffrows();
    private static WorkSchedule ToDomain(WorkScheduleRecord record) => new(record.Title, record.StartTime, record.EndTime, record.Description, record.Location) { Id = record.Id };
    private static WorkScheduleRecord ToRecord(WorkSchedule item, DateTime created, DateTime modified) => new() { Id = item.Id, Title = item.Title, Description = item.Description, Location = item.Location, StartTime = item.StartTime, EndTime = item.EndTime, CreatedTime = created, ModifiedTime = modified };
}
