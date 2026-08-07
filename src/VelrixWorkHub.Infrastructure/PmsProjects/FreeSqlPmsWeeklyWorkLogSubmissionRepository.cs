using FreeSql;
using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmsProjects;

public sealed class FreeSqlPmsWeeklyWorkLogSubmissionRepository(IFreeSql fsql) : IPmsWeeklyWorkLogSubmissionRepository
{
    public IReadOnlyList<PmsWeeklyWorkLogSubmission> List(Guid? projectId = null)
    {
        var query = fsql.Select<PmsWeeklyWorkLogSubmissionRecord>();
        if (projectId is Guid id) query = query.Where(x => x.ProjectId == id);
        return query.ToList().Select(x => PmsWeeklyWorkLogSubmission.Restore(x.Id, x.ProjectId, x.MemberName, DateOnly.FromDateTime(x.WeekStart), x.SnapshotJson, x.TotalHours, x.Status, x.SubmittedBy, x.SubmittedAt, x.RejectionReason)).ToArray();
    }
    public void Add(PmsWeeklyWorkLogSubmission item)
    {
        try { fsql.Insert(ToRecord(item, DateTime.Now, DateTime.Now)).ExecuteAffrows(); }
        catch (Exception exception) when (PmsWeeklyWorkLogSubmissionSchemaMigration.IsActiveWeekUniquenessViolation(exception))
        {
            throw new InvalidOperationException("该成员本周工时已提交审批或已批准。", exception);
        }
    }
    public void Update(PmsWeeklyWorkLogSubmission item)
    {
        int rows;
        try { rows = fsql.Update<PmsWeeklyWorkLogSubmissionRecord>().SetSource(ToRecord(item, DateTime.MinValue, DateTime.Now)).IgnoreColumns(x => new { x.CreatedTime }).Where(x => x.Id == item.Id).ExecuteAffrows(); }
        catch (Exception exception) when (PmsWeeklyWorkLogSubmissionSchemaMigration.IsActiveWeekUniquenessViolation(exception))
        {
            throw new InvalidOperationException("该成员本周工时已提交审批或已批准。", exception);
        }
        if (rows == 0) throw new InvalidOperationException("工时周报不存在或已被删除。");
    }
    public void Remove(Guid id) => fsql.Delete<PmsWeeklyWorkLogSubmissionRecord>(id).ExecuteAffrows();
    private static PmsWeeklyWorkLogSubmissionRecord ToRecord(PmsWeeklyWorkLogSubmission x, DateTime created, DateTime modified) => new() { Id = x.Id, ProjectId = x.ProjectId, MemberName = x.MemberName, WeekStart = x.WeekStart.ToDateTime(TimeOnly.MinValue), SnapshotJson = x.SnapshotJson, TotalHours = x.TotalHours, Status = x.Status, SubmittedBy = x.SubmittedBy, SubmittedAt = x.SubmittedAt, RejectionReason = x.RejectionReason, ActiveWeekKey = PmsWeeklyWorkLogSubmissionSchemaMigration.GetActiveWeekKey(x.ProjectId, x.MemberName, x.WeekStart, x.Status), CreatedTime = created, ModifiedTime = modified };
}
