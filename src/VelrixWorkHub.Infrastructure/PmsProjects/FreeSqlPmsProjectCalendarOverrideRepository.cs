using FreeSql;
using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmsProjects;

public sealed class FreeSqlPmsProjectCalendarOverrideRepository(IFreeSql fsql) : IPmsProjectCalendarOverrideRepository
{
    public IReadOnlyList<PmsProjectCalendarOverride> List(Guid projectId) => fsql.Select<PmsProjectCalendarOverrideRecord>().Where(x => x.ProjectId == projectId).OrderBy(x => x.Date).ToList().Select(x => new PmsProjectCalendarOverride(x.ProjectId, DateOnly.FromDateTime(x.Date), x.IsWorkingDay, x.Note) { Id = x.Id }).ToArray();
    public void Add(PmsProjectCalendarOverride item) { var now = DateTime.Now; fsql.Insert(new PmsProjectCalendarOverrideRecord { Id = item.Id, ProjectId = item.ProjectId, Date = item.Date.ToDateTime(TimeOnly.MinValue), IsWorkingDay = item.IsWorkingDay, Note = item.Note, CreatedTime = now, ModifiedTime = now }).ExecuteAffrows(); }
    public void Update(PmsProjectCalendarOverride item) { var rows = fsql.Update<PmsProjectCalendarOverrideRecord>().Set(x => x.IsWorkingDay, item.IsWorkingDay).Set(x => x.Note, item.Note).Set(x => x.ModifiedTime, DateTime.Now).Where(x => x.Id == item.Id).ExecuteAffrows(); if (rows == 0) throw new InvalidOperationException("项目日历覆盖不存在或已被删除。"); }
    public void Remove(Guid id) => fsql.Delete<PmsProjectCalendarOverrideRecord>().Where(x => x.Id == id).ExecuteAffrows();
}
