using FreeSql;
using VelrixWorkHub.Application.PmpProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmpProjects;

public sealed class FreeSqlPmpProjectCalendarOverrideRepository(IFreeSql fsql) : IPmpProjectCalendarOverrideRepository
{
    public IReadOnlyList<PmpProjectCalendarOverride> List(Guid projectId) => fsql.Select<PmpProjectCalendarOverrideRecord>().Where(x => x.ProjectId == projectId).OrderBy(x => x.Date).ToList().Select(x => new PmpProjectCalendarOverride(x.ProjectId, DateOnly.FromDateTime(x.Date), x.IsWorkingDay, x.Note) { Id = x.Id }).ToArray();
    public void Add(PmpProjectCalendarOverride item) { var now = DateTime.Now; fsql.Insert(new PmpProjectCalendarOverrideRecord { Id = item.Id, ProjectId = item.ProjectId, Date = item.Date.ToDateTime(TimeOnly.MinValue), IsWorkingDay = item.IsWorkingDay, Note = item.Note, CreatedTime = now, ModifiedTime = now }).ExecuteAffrows(); }
    public void Update(PmpProjectCalendarOverride item) { var rows = fsql.Update<PmpProjectCalendarOverrideRecord>().Set(x => x.IsWorkingDay, item.IsWorkingDay).Set(x => x.Note, item.Note).Set(x => x.ModifiedTime, DateTime.Now).Where(x => x.Id == item.Id).ExecuteAffrows(); if (rows == 0) throw new InvalidOperationException("项目日历覆盖不存在或已被删除。"); }
    public void Remove(Guid id) => fsql.Delete<PmpProjectCalendarOverrideRecord>().Where(x => x.Id == id).ExecuteAffrows();
}
