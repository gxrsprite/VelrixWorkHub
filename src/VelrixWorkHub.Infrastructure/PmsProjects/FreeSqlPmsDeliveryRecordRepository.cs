using FreeSql;
using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmsProjects;

public sealed class FreeSqlPmsDeliveryRecordRepository(IFreeSql fsql) : IPmsDeliveryRecordRepository
{
    public IReadOnlyList<PmsDeliveryRecord> List(Guid? projectId = null)
    {
        var query = fsql.Select<PmsDeliveryRecordRecord>();
        if (projectId is Guid id) query = query.Where(x => x.ProjectId == id);
        return query.OrderBy(x => x.Type).OrderBy(x => x.Status).ToList().Select(ToDomain).ToArray();
    }
    public void Add(PmsDeliveryRecord item) { var now = DateTime.Now; fsql.Insert(ToRecord(item, now, now)).ExecuteAffrows(); }
    public void Update(PmsDeliveryRecord item)
    {
        var rows = fsql.Update<PmsDeliveryRecordRecord>().SetSource(ToRecord(item, DateTime.MinValue, DateTime.Now)).IgnoreColumns(x => new { x.CreatedTime }).Where(x => x.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("交付记录不存在或已被删除。");
    }
    public void Remove(Guid id) => fsql.Delete<PmsDeliveryRecordRecord>().Where(x => x.Id == id).ExecuteAffrows();
    private static PmsDeliveryRecord ToDomain(PmsDeliveryRecordRecord x) => PmsDeliveryRecord.Restore(x.Id, x.ProjectId, x.RequirementId, x.WbsTaskId, x.RecordNo, x.Type, x.Title, x.Description, x.OwnerName, x.Status, x.ReviewConclusion, x.ReleaseVersion, x.ReleaseResult, x.OtherInfo);
    private static PmsDeliveryRecordRecord ToRecord(PmsDeliveryRecord x, DateTime created, DateTime modified) => new() { Id = x.Id, ProjectId = x.ProjectId, RequirementId = x.RequirementId, WbsTaskId = x.WbsTaskId, RecordNo = x.RecordNo, Type = x.Type, Title = x.Title, Description = x.Description, OwnerName = x.OwnerName, Status = x.Status, ReviewConclusion = x.ReviewConclusion, ReleaseVersion = x.ReleaseVersion, ReleaseResult = x.ReleaseResult, OtherInfo = x.OtherInfo, CreatedTime = created, ModifiedTime = modified };
}

public sealed class FreeSqlPmsDeliveryRecordStatusHistoryRepository(IFreeSql fsql) : IPmsDeliveryRecordStatusHistoryRepository
{
    public IReadOnlyList<PmsDeliveryRecordStatusHistory> List(Guid deliveryRecordId) => fsql.Select<PmsDeliveryRecordStatusHistoryRecord>().Where(x => x.DeliveryRecordId == deliveryRecordId).OrderByDescending(x => x.OccurredAt).ToList().Select(x => PmsDeliveryRecordStatusHistory.Restore(x.Id, x.DeliveryRecordId, x.Status, x.Note, x.ActorName, x.OccurredAt)).ToArray();
    public void Add(PmsDeliveryRecordStatusHistory item) => fsql.Insert(new PmsDeliveryRecordStatusHistoryRecord { Id = item.Id, DeliveryRecordId = item.DeliveryRecordId, Status = item.Status, Note = item.Note, ActorName = item.ActorName, OccurredAt = item.OccurredAt }).ExecuteAffrows();
}
