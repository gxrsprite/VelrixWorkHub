using FreeSql;
using VelrixWorkHub.Application.PmpProjects;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.PmpProjects;
public sealed class FreeSqlPmpWorkLogRepository(IFreeSql fsql) : IPmpWorkLogRepository
{
    public IReadOnlyList<PmpWorkLog> List(Guid? projectId = null) { var query = fsql.Select<PmpWorkLogRecord>(); if (projectId is not null) query = query.Where(x => x.ProjectId == projectId); return query.ToList().Select(ToDomain).ToArray(); }
    public void Add(PmpWorkLog item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(PmpWorkLog item) { var rows = fsql.Update<PmpWorkLogRecord>().SetSource(ToRecord(item)).Where(x => x.Id == item.Id).ExecuteAffrows(); if (rows == 0) throw new InvalidOperationException("工时记录不存在或已被删除。"); }
    public void Remove(Guid id) => fsql.Delete<PmpWorkLogRecord>().Where(x => x.Id == id).ExecuteAffrows();
    private static PmpWorkLog ToDomain(PmpWorkLogRecord x) => new(x.ProjectId, x.WbsTaskId, DateOnly.FromDateTime(x.WorkDate), x.MemberName, x.Hours, x.Note, x.AttendanceStatus ?? PmpWorkLogAttendanceStatus.Normal) { Id = x.Id };
    private static PmpWorkLogRecord ToRecord(PmpWorkLog x) => new() { Id = x.Id, ProjectId = x.ProjectId, WbsTaskId = x.WbsTaskId, WorkDate = x.WorkDate.ToDateTime(TimeOnly.MinValue), MemberName = x.MemberName, Hours = x.Hours, Note = x.Note, AttendanceStatus = x.AttendanceStatus };
}
