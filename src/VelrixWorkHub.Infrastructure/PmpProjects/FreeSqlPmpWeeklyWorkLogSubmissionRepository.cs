using FreeSql;
using VelrixWorkHub.Application.PmpProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmpProjects;

public sealed class FreeSqlPmpWeeklyWorkLogSubmissionRepository(IFreeSql fsql) : IPmpWeeklyWorkLogSubmissionRepository
{
    public IReadOnlyList<PmpWeeklyWorkLogSubmission> List(Guid? projectId = null)
    {
        var query = fsql.Select<PmpWeeklyWorkLogSubmissionRecord>();
        if (projectId is Guid id) query = query.Where(x => x.ProjectId == id);
        return query.ToList().Select(x => PmpWeeklyWorkLogSubmission.Restore(x.Id, x.ProjectId, x.MemberName, DateOnly.FromDateTime(x.WeekStart), x.SnapshotJson, x.TotalHours, x.Status, x.SubmittedBy, x.SubmittedAt, x.RejectionReason)).ToArray();
    }
    public void Add(PmpWeeklyWorkLogSubmission item)
    {
        try { fsql.Insert(ToRecord(item, DateTime.Now, DateTime.Now)).ExecuteAffrows(); }
        catch (Exception exception) when (PmpWeeklyWorkLogSubmissionSchemaMigration.IsActiveWeekUniquenessViolation(exception))
        {
            throw new InvalidOperationException("该成员本周工时已提交审批或已批准。", exception);
        }
    }
    public void Update(PmpWeeklyWorkLogSubmission item)
    {
        int rows;
        try { rows = fsql.Update<PmpWeeklyWorkLogSubmissionRecord>().SetSource(ToRecord(item, DateTime.MinValue, DateTime.Now)).IgnoreColumns(x => new { x.CreatedTime }).Where(x => x.Id == item.Id).ExecuteAffrows(); }
        catch (Exception exception) when (PmpWeeklyWorkLogSubmissionSchemaMigration.IsActiveWeekUniquenessViolation(exception))
        {
            throw new InvalidOperationException("该成员本周工时已提交审批或已批准。", exception);
        }
        if (rows == 0) throw new InvalidOperationException("工时周报不存在或已被删除。");
    }
    public void Remove(Guid id) => fsql.Delete<PmpWeeklyWorkLogSubmissionRecord>(id).ExecuteAffrows();
    private static PmpWeeklyWorkLogSubmissionRecord ToRecord(PmpWeeklyWorkLogSubmission x, DateTime created, DateTime modified) => new() { Id = x.Id, ProjectId = x.ProjectId, MemberName = x.MemberName, WeekStart = x.WeekStart.ToDateTime(TimeOnly.MinValue), SnapshotJson = x.SnapshotJson, TotalHours = x.TotalHours, Status = x.Status, SubmittedBy = x.SubmittedBy, SubmittedAt = x.SubmittedAt, RejectionReason = x.RejectionReason, ActiveWeekKey = PmpWeeklyWorkLogSubmissionSchemaMigration.GetActiveWeekKey(x.ProjectId, x.MemberName, x.WeekStart, x.Status), CreatedTime = created, ModifiedTime = modified };
}
