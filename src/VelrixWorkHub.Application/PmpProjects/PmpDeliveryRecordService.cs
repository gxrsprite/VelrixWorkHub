using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmpProjects;

public interface IPmpDeliveryRecordRepository
{
    IReadOnlyList<PmpDeliveryRecord> List(Guid? projectId = null);
    void Add(PmpDeliveryRecord item);
    void Update(PmpDeliveryRecord item);
    void Remove(Guid id);
}

public interface IPmpDeliveryRecordStatusHistoryRepository
{
    IReadOnlyList<PmpDeliveryRecordStatusHistory> List(Guid deliveryRecordId);
    void Add(PmpDeliveryRecordStatusHistory item);
}

public sealed class PmpDeliveryRecordService(IPmpDeliveryRecordRepository repository, IPmpDeliveryRecordStatusHistoryRepository histories, IPmpProjectRepository projects, IPmpRequirementRepository requirements, IPmpWbsTaskRepository wbsTasks)
{
    public IReadOnlyList<PmpDeliveryRecord> List(Guid? projectId = null, string? keyword = null, PmpDeliveryRecordType? type = null)
    {
        var text = keyword?.Trim();
        return repository.List(projectId).Where(x => (type is null || x.Type == type) && (string.IsNullOrWhiteSpace(text) || x.RecordNo.Contains(text, StringComparison.OrdinalIgnoreCase) || x.Title.Contains(text, StringComparison.OrdinalIgnoreCase) || (x.OwnerName?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false))).OrderBy(x => x.Type).ThenBy(x => x.Status).ThenBy(x => x.RecordNo).ToArray();
    }

    public IReadOnlyList<PmpDeliveryRecordStatusHistory> ListHistory(Guid deliveryRecordId) => histories.List(deliveryRecordId).OrderByDescending(x => x.OccurredAt).ToArray();

    public PmpDeliveryRecord Create(Guid projectId, Guid? requirementId, Guid? wbsTaskId, string recordNo, PmpDeliveryRecordType type, string title, string? description, string? ownerName, string? reviewConclusion, string? releaseVersion, string? releaseResult, string? otherInfo, string? actorName)
    {
        EnsureReferences(projectId, requirementId, wbsTaskId);
        var item = new PmpDeliveryRecord(projectId, requirementId, wbsTaskId, recordNo, type, title, description, ownerName, reviewConclusion, releaseVersion, releaseResult, otherInfo);
        EnsureUnique(item); repository.Add(item); histories.Add(new PmpDeliveryRecordStatusHistory(item.Id, item.Status, "创建记录", actorName, DateTime.Now)); return item;
    }

    public void Edit(PmpDeliveryRecord item, Guid? requirementId, Guid? wbsTaskId, string recordNo, PmpDeliveryRecordType type, string title, string? description, string? ownerName, string? reviewConclusion, string? releaseVersion, string? releaseResult, string? otherInfo)
    {
        if (item.Status is PmpDeliveryRecordStatus.Closed or PmpDeliveryRecordStatus.Released or PmpDeliveryRecordStatus.Withdrawn) throw new InvalidOperationException("终态交付记录不能编辑。");
        EnsureReferences(item.ProjectId, requirementId, wbsTaskId); EnsureUnique(item.ProjectId, item.Id, recordNo);
        item.Edit(item.ProjectId, requirementId, wbsTaskId, recordNo, type, title, description, ownerName, reviewConclusion, releaseVersion, releaseResult, otherInfo); repository.Update(item);
    }

    public void SetStatus(PmpDeliveryRecord item, PmpDeliveryRecordStatus status, string? note, string? actorName)
    {
        item.SetStatus(status); repository.Update(item); histories.Add(new PmpDeliveryRecordStatusHistory(item.Id, status, note, actorName, DateTime.Now));
    }

    public void Remove(PmpDeliveryRecord item)
    {
        if (item.Status != PmpDeliveryRecordStatus.New) throw new InvalidOperationException("只有新建交付记录可以删除。");
        repository.Remove(item.Id);
    }

    private void EnsureReferences(Guid projectId, Guid? requirementId, Guid? wbsTaskId)
    {
        if (!projects.List().Any(x => x.Id == projectId)) throw new InvalidOperationException("关联项目不存在。");
        if (requirementId is Guid requirement && !requirements.List(projectId).Any(x => x.Id == requirement)) throw new InvalidOperationException("关联需求不存在或不属于当前项目。");
        if (wbsTaskId is Guid wbsTask && !wbsTasks.List(projectId).Any(x => x.Id == wbsTask)) throw new InvalidOperationException("关联 WBS 任务不存在或不属于当前项目。");
    }

    private void EnsureUnique(PmpDeliveryRecord item) => EnsureUnique(item.ProjectId, item.Id, item.RecordNo);
    private void EnsureUnique(Guid projectId, Guid currentId, string recordNo) { if (repository.List(projectId).Any(x => x.Id != currentId && x.RecordNo.Equals(recordNo.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("同一项目下交付记录编号已存在。"); }
}
