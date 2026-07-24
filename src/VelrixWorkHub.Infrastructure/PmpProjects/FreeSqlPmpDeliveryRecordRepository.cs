using FreeSql;
using VelrixWorkHub.Application.PmpProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmpProjects;

public sealed class FreeSqlPmpDeliveryRecordRepository(IFreeSql fsql) : IPmpDeliveryRecordRepository
{
    public IReadOnlyList<PmpDeliveryRecord> List(Guid? projectId = null)
    {
        var query = fsql.Select<PmpDeliveryRecordRecord>();
        if (projectId is Guid id) query = query.Where(x => x.ProjectId == id);
        return query.OrderBy(x => x.Type).OrderBy(x => x.Status).ToList().Select(ToDomain).ToArray();
    }
    public void Add(PmpDeliveryRecord item) { var now = DateTime.Now; fsql.Insert(ToRecord(item, now, now)).ExecuteAffrows(); }
    public void Update(PmpDeliveryRecord item)
    {
        var rows = fsql.Update<PmpDeliveryRecordRecord>().SetSource(ToRecord(item, DateTime.MinValue, DateTime.Now)).IgnoreColumns(x => new { x.CreatedTime }).Where(x => x.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("交付记录不存在或已被删除。");
    }
    public void Remove(Guid id) => fsql.Delete<PmpDeliveryRecordRecord>().Where(x => x.Id == id).ExecuteAffrows();
    private static PmpDeliveryRecord ToDomain(PmpDeliveryRecordRecord x) => PmpDeliveryRecord.Restore(x.Id, x.ProjectId, x.RequirementId, x.WbsTaskId, x.RecordNo, x.Type, x.Title, x.Description, x.OwnerName, x.Status, x.ReviewConclusion, x.ReleaseVersion, x.ReleaseResult, x.OtherInfo);
    private static PmpDeliveryRecordRecord ToRecord(PmpDeliveryRecord x, DateTime created, DateTime modified) => new() { Id = x.Id, ProjectId = x.ProjectId, RequirementId = x.RequirementId, WbsTaskId = x.WbsTaskId, RecordNo = x.RecordNo, Type = x.Type, Title = x.Title, Description = x.Description, OwnerName = x.OwnerName, Status = x.Status, ReviewConclusion = x.ReviewConclusion, ReleaseVersion = x.ReleaseVersion, ReleaseResult = x.ReleaseResult, OtherInfo = x.OtherInfo, CreatedTime = created, ModifiedTime = modified };
}

public sealed class FreeSqlPmpDeliveryRecordStatusHistoryRepository(IFreeSql fsql) : IPmpDeliveryRecordStatusHistoryRepository
{
    public IReadOnlyList<PmpDeliveryRecordStatusHistory> List(Guid deliveryRecordId) => fsql.Select<PmpDeliveryRecordStatusHistoryRecord>().Where(x => x.DeliveryRecordId == deliveryRecordId).OrderByDescending(x => x.OccurredAt).ToList().Select(x => PmpDeliveryRecordStatusHistory.Restore(x.Id, x.DeliveryRecordId, x.Status, x.Note, x.ActorName, x.OccurredAt)).ToArray();
    public void Add(PmpDeliveryRecordStatusHistory item) => fsql.Insert(new PmpDeliveryRecordStatusHistoryRecord { Id = item.Id, DeliveryRecordId = item.DeliveryRecordId, Status = item.Status, Note = item.Note, ActorName = item.ActorName, OccurredAt = item.OccurredAt }).ExecuteAffrows();
}
