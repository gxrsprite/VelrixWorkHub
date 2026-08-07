namespace VelrixWorkHub.Domain;

public enum PmsDeliveryRecordType { Defect, Review, Release }
public enum PmsDeliveryRecordStatus { New, InProgress, Resolved, Passed, Failed, Released, Withdrawn, Closed }

public sealed class PmsDeliveryRecord
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ProjectId { get; private set; }
    public Guid? RequirementId { get; private set; }
    public Guid? WbsTaskId { get; private set; }
    public string RecordNo { get; private set; } = string.Empty;
    public PmsDeliveryRecordType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? OwnerName { get; private set; }
    public PmsDeliveryRecordStatus Status { get; private set; }
    public string? ReviewConclusion { get; private set; }
    public string? ReleaseVersion { get; private set; }
    public string? ReleaseResult { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public PmsDeliveryRecord(Guid projectId, Guid? requirementId, Guid? wbsTaskId, string recordNo, PmsDeliveryRecordType type, string title, string? description, string? ownerName, string? reviewConclusion, string? releaseVersion, string? releaseResult, string? otherInfo)
    {
        Edit(projectId, requirementId, wbsTaskId, recordNo, type, title, description, ownerName, reviewConclusion, releaseVersion, releaseResult, otherInfo);
        Status = PmsDeliveryRecordStatus.New;
    }

    public static PmsDeliveryRecord Restore(Guid id, Guid projectId, Guid? requirementId, Guid? wbsTaskId, string recordNo, PmsDeliveryRecordType type, string title, string? description, string? ownerName, PmsDeliveryRecordStatus status, string? reviewConclusion, string? releaseVersion, string? releaseResult, string? otherInfo)
        => new(projectId, requirementId, wbsTaskId, recordNo, type, title, description, ownerName, reviewConclusion, releaseVersion, releaseResult, otherInfo) { Id = id, Status = status };

    public void Edit(Guid projectId, Guid? requirementId, Guid? wbsTaskId, string recordNo, PmsDeliveryRecordType type, string title, string? description, string? ownerName, string? reviewConclusion, string? releaseVersion, string? releaseResult, string? otherInfo)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("必须关联项目。", nameof(projectId));
        if (string.IsNullOrWhiteSpace(recordNo)) throw new ArgumentException("交付记录编号不能为空。", nameof(recordNo));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("交付记录标题不能为空。", nameof(title));
        if (type is PmsDeliveryRecordType.Defect or PmsDeliveryRecordType.Review && requirementId is null) throw new ArgumentException("缺陷和评审必须关联需求。", nameof(requirementId));
        if (type == PmsDeliveryRecordType.Release && string.IsNullOrWhiteSpace(releaseVersion)) throw new ArgumentException("发布记录必须填写版本号。", nameof(releaseVersion));
        ProjectId = projectId; RequirementId = requirementId; WbsTaskId = wbsTaskId; RecordNo = recordNo.Trim(); Type = type; Title = title.Trim(); Description = Clean(description); OwnerName = Clean(ownerName); ReviewConclusion = Clean(reviewConclusion); ReleaseVersion = Clean(releaseVersion); ReleaseResult = Clean(releaseResult); OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public void SetStatus(PmsDeliveryRecordStatus status)
    {
        if (status == Status) return;
        var allowed = (Type, Status, status) switch
        {
            (PmsDeliveryRecordType.Defect, PmsDeliveryRecordStatus.New, PmsDeliveryRecordStatus.InProgress) => true,
            (PmsDeliveryRecordType.Defect, PmsDeliveryRecordStatus.InProgress, PmsDeliveryRecordStatus.Resolved) => true,
            (PmsDeliveryRecordType.Defect, PmsDeliveryRecordStatus.Resolved, PmsDeliveryRecordStatus.Closed) => true,
            (PmsDeliveryRecordType.Review, PmsDeliveryRecordStatus.New, PmsDeliveryRecordStatus.Passed) => true,
            (PmsDeliveryRecordType.Review, PmsDeliveryRecordStatus.New, PmsDeliveryRecordStatus.Failed) => true,
            (PmsDeliveryRecordType.Release, PmsDeliveryRecordStatus.New, PmsDeliveryRecordStatus.Released) => true,
            (PmsDeliveryRecordType.Release, PmsDeliveryRecordStatus.New, PmsDeliveryRecordStatus.Withdrawn) => true,
            _ => false
        };
        if (!allowed) throw new InvalidOperationException($"{Type}记录不能从“{Status}”变更为“{status}”。");
        if (Type == PmsDeliveryRecordType.Review && status is PmsDeliveryRecordStatus.Passed or PmsDeliveryRecordStatus.Failed && string.IsNullOrWhiteSpace(ReviewConclusion)) throw new ArgumentException("完成评审必须填写评审结论。", nameof(status));
        if (Type == PmsDeliveryRecordType.Release && status == PmsDeliveryRecordStatus.Released && string.IsNullOrWhiteSpace(ReleaseResult)) throw new ArgumentException("发布版本必须填写发布结果。", nameof(status));
        Status = status;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class PmsDeliveryRecordStatusHistory
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid DeliveryRecordId { get; init; }
    public PmsDeliveryRecordStatus Status { get; init; }
    public string? Note { get; init; }
    public string? ActorName { get; init; }
    public DateTime OccurredAt { get; init; }

    public PmsDeliveryRecordStatusHistory(Guid deliveryRecordId, PmsDeliveryRecordStatus status, string? note, string? actorName, DateTime occurredAt)
    {
        if (deliveryRecordId == Guid.Empty) throw new ArgumentException("必须关联交付记录。", nameof(deliveryRecordId));
        DeliveryRecordId = deliveryRecordId; Status = status; Note = Clean(note); ActorName = Clean(actorName); OccurredAt = occurredAt;
    }

    public static PmsDeliveryRecordStatusHistory Restore(Guid id, Guid deliveryRecordId, PmsDeliveryRecordStatus status, string? note, string? actorName, DateTime occurredAt)
        => new(deliveryRecordId, status, note, actorName, occurredAt) { Id = id };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
