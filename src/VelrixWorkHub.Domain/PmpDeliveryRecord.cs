namespace VelrixWorkHub.Domain;

public enum PmpDeliveryRecordType { Defect, Review, Release }
public enum PmpDeliveryRecordStatus { New, InProgress, Resolved, Passed, Failed, Released, Withdrawn, Closed }

public sealed class PmpDeliveryRecord
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ProjectId { get; private set; }
    public Guid? RequirementId { get; private set; }
    public Guid? WbsTaskId { get; private set; }
    public string RecordNo { get; private set; } = string.Empty;
    public PmpDeliveryRecordType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? OwnerName { get; private set; }
    public PmpDeliveryRecordStatus Status { get; private set; }
    public string? ReviewConclusion { get; private set; }
    public string? ReleaseVersion { get; private set; }
    public string? ReleaseResult { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public PmpDeliveryRecord(Guid projectId, Guid? requirementId, Guid? wbsTaskId, string recordNo, PmpDeliveryRecordType type, string title, string? description, string? ownerName, string? reviewConclusion, string? releaseVersion, string? releaseResult, string? otherInfo)
    {
        Edit(projectId, requirementId, wbsTaskId, recordNo, type, title, description, ownerName, reviewConclusion, releaseVersion, releaseResult, otherInfo);
        Status = PmpDeliveryRecordStatus.New;
    }

    public static PmpDeliveryRecord Restore(Guid id, Guid projectId, Guid? requirementId, Guid? wbsTaskId, string recordNo, PmpDeliveryRecordType type, string title, string? description, string? ownerName, PmpDeliveryRecordStatus status, string? reviewConclusion, string? releaseVersion, string? releaseResult, string? otherInfo)
        => new(projectId, requirementId, wbsTaskId, recordNo, type, title, description, ownerName, reviewConclusion, releaseVersion, releaseResult, otherInfo) { Id = id, Status = status };

    public void Edit(Guid projectId, Guid? requirementId, Guid? wbsTaskId, string recordNo, PmpDeliveryRecordType type, string title, string? description, string? ownerName, string? reviewConclusion, string? releaseVersion, string? releaseResult, string? otherInfo)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("必须关联项目。", nameof(projectId));
        if (string.IsNullOrWhiteSpace(recordNo)) throw new ArgumentException("交付记录编号不能为空。", nameof(recordNo));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("交付记录标题不能为空。", nameof(title));
        if (type is PmpDeliveryRecordType.Defect or PmpDeliveryRecordType.Review && requirementId is null) throw new ArgumentException("缺陷和评审必须关联需求。", nameof(requirementId));
        if (type == PmpDeliveryRecordType.Release && string.IsNullOrWhiteSpace(releaseVersion)) throw new ArgumentException("发布记录必须填写版本号。", nameof(releaseVersion));
        ProjectId = projectId; RequirementId = requirementId; WbsTaskId = wbsTaskId; RecordNo = recordNo.Trim(); Type = type; Title = title.Trim(); Description = Clean(description); OwnerName = Clean(ownerName); ReviewConclusion = Clean(reviewConclusion); ReleaseVersion = Clean(releaseVersion); ReleaseResult = Clean(releaseResult); OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public void SetStatus(PmpDeliveryRecordStatus status)
    {
        if (status == Status) return;
        var allowed = (Type, Status, status) switch
        {
            (PmpDeliveryRecordType.Defect, PmpDeliveryRecordStatus.New, PmpDeliveryRecordStatus.InProgress) => true,
            (PmpDeliveryRecordType.Defect, PmpDeliveryRecordStatus.InProgress, PmpDeliveryRecordStatus.Resolved) => true,
            (PmpDeliveryRecordType.Defect, PmpDeliveryRecordStatus.Resolved, PmpDeliveryRecordStatus.Closed) => true,
            (PmpDeliveryRecordType.Review, PmpDeliveryRecordStatus.New, PmpDeliveryRecordStatus.Passed) => true,
            (PmpDeliveryRecordType.Review, PmpDeliveryRecordStatus.New, PmpDeliveryRecordStatus.Failed) => true,
            (PmpDeliveryRecordType.Release, PmpDeliveryRecordStatus.New, PmpDeliveryRecordStatus.Released) => true,
            (PmpDeliveryRecordType.Release, PmpDeliveryRecordStatus.New, PmpDeliveryRecordStatus.Withdrawn) => true,
            _ => false
        };
        if (!allowed) throw new InvalidOperationException($"{Type}记录不能从“{Status}”变更为“{status}”。");
        if (Type == PmpDeliveryRecordType.Review && status is PmpDeliveryRecordStatus.Passed or PmpDeliveryRecordStatus.Failed && string.IsNullOrWhiteSpace(ReviewConclusion)) throw new ArgumentException("完成评审必须填写评审结论。", nameof(status));
        if (Type == PmpDeliveryRecordType.Release && status == PmpDeliveryRecordStatus.Released && string.IsNullOrWhiteSpace(ReleaseResult)) throw new ArgumentException("发布版本必须填写发布结果。", nameof(status));
        Status = status;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class PmpDeliveryRecordStatusHistory
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid DeliveryRecordId { get; init; }
    public PmpDeliveryRecordStatus Status { get; init; }
    public string? Note { get; init; }
    public string? ActorName { get; init; }
    public DateTime OccurredAt { get; init; }

    public PmpDeliveryRecordStatusHistory(Guid deliveryRecordId, PmpDeliveryRecordStatus status, string? note, string? actorName, DateTime occurredAt)
    {
        if (deliveryRecordId == Guid.Empty) throw new ArgumentException("必须关联交付记录。", nameof(deliveryRecordId));
        DeliveryRecordId = deliveryRecordId; Status = status; Note = Clean(note); ActorName = Clean(actorName); OccurredAt = occurredAt;
    }

    public static PmpDeliveryRecordStatusHistory Restore(Guid id, Guid deliveryRecordId, PmpDeliveryRecordStatus status, string? note, string? actorName, DateTime occurredAt)
        => new(deliveryRecordId, status, note, actorName, occurredAt) { Id = id };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
