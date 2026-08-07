using FreeSql;
using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.PmsProjects;
public sealed class FreeSqlPmsWorkLogRepository(IFreeSql fsql) : IPmsWorkLogRepository
{
    public IReadOnlyList<PmsWorkLog> List(Guid? projectId = null) { var query = fsql.Select<PmsWorkLogRecord>(); if (projectId is not null) query = query.Where(x => x.ProjectId == projectId); return query.ToList().Select(ToDomain).ToArray(); }
    public void Add(PmsWorkLog item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(PmsWorkLog item) { var rows = fsql.Update<PmsWorkLogRecord>().SetSource(ToRecord(item)).Where(x => x.Id == item.Id).ExecuteAffrows(); if (rows == 0) throw new InvalidOperationException("工时记录不存在或已被删除。"); }
    public void Remove(Guid id) => fsql.Delete<PmsWorkLogRecord>().Where(x => x.Id == id).ExecuteAffrows();
    private static PmsWorkLog ToDomain(PmsWorkLogRecord x) => new(x.ProjectId, x.WbsTaskId, DateOnly.FromDateTime(x.WorkDate), x.MemberName, x.Hours, x.Note, x.AttendanceStatus ?? PmsWorkLogAttendanceStatus.Normal) { Id = x.Id };
    private static PmsWorkLogRecord ToRecord(PmsWorkLog x) => new() { Id = x.Id, ProjectId = x.ProjectId, WbsTaskId = x.WbsTaskId, WorkDate = x.WorkDate.ToDateTime(TimeOnly.MinValue), MemberName = x.MemberName, Hours = x.Hours, Note = x.Note, AttendanceStatus = x.AttendanceStatus };
}
