using FreeSql;
using VelrixWorkHub.Application.PmpProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmpProjects;

public sealed class FreeSqlPmpProjectWorkItemRepository(IFreeSql fsql) : IPmpProjectWorkItemRepository
{
    public IReadOnlyList<PmpProjectWorkItem> List(Guid? projectId = null) { var query = fsql.Select<PmpProjectWorkItemRecord>(); if (projectId is Guid id) query = query.Where(x => x.ProjectId == id); return query.OrderBy(x => x.Status).OrderByDescending(x => x.Priority).ToList().Select(ToDomain).ToArray(); }
    public void Add(PmpProjectWorkItem item) => fsql.Insert(ToRecord(item, DateTime.Now, DateTime.Now)).ExecuteAffrows();
    public void Update(PmpProjectWorkItem item)
    {
        var rows = fsql.Update<PmpProjectWorkItemRecord>().SetSource(ToRecord(item, DateTime.MinValue, DateTime.Now)).IgnoreColumns(x => new { x.CreatedTime }).Where(x => x.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("工作项不存在或已被删除。");
    }
    public void Remove(Guid id) => fsql.Delete<PmpProjectWorkItemRecord>().Where(x => x.Id == id).ExecuteAffrows();
    private static PmpProjectWorkItem ToDomain(PmpProjectWorkItemRecord x) => PmpProjectWorkItem.Restore(x.Id, x.ProjectId, x.ParentId, x.SourceType, x.SourceId, x.Title, x.Description, x.OwnerName, x.ParticipantNames, x.Priority, x.Status, x.PlannedStartAt, x.PlannedEndAt, x.ActualStartAt, x.ActualEndAt, x.Feedback, x.OtherInfo, x.OwnerUserId, x.ReminderAt, x.CompletionRejectionReason, x.ParticipantUserIdsJson, x.VisibilityOrganizationIdsJson, x.VisibilityRoleIdsJson);
    private static PmpProjectWorkItemRecord ToRecord(PmpProjectWorkItem x, DateTime created, DateTime modified) => new() { Id = x.Id, ProjectId = x.ProjectId, ParentId = x.ParentId, SourceType = x.SourceType, SourceId = x.SourceId, Title = x.Title, Description = x.Description, OwnerUserId = x.OwnerUserId, OwnerName = x.OwnerName, ParticipantUserIdsJson = x.ParticipantUserIdsJson, ParticipantNames = x.ParticipantNames, VisibilityOrganizationIdsJson = x.VisibilityOrganizationIdsJson, VisibilityRoleIdsJson = x.VisibilityRoleIdsJson, Priority = x.Priority, Status = x.Status, PlannedStartAt = x.PlannedStartAt, PlannedEndAt = x.PlannedEndAt, ReminderAt = x.ReminderAt, ActualStartAt = x.ActualStartAt, ActualEndAt = x.ActualEndAt, Feedback = x.Feedback, CompletionRejectionReason = x.CompletionRejectionReason, OtherInfo = x.OtherInfo, CreatedTime = created, ModifiedTime = modified };
}
