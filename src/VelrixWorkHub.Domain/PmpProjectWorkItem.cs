using System.Text.Json;

namespace VelrixWorkHub.Domain;

public enum PmpProjectWorkItemPriority { Low, Medium, High, Critical }
public enum PmpProjectWorkItemStatus { Draft, Open, InProgress, PendingApproval, Completed, Cancelled }

public sealed class PmpProjectWorkItem
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ProjectId { get; private set; }
    public Guid? ParentId { get; private set; }
    public string? SourceType { get; private set; }
    public Guid? SourceId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid? OwnerUserId { get; private set; }
    public string? OwnerName { get; private set; }
    public string ParticipantUserIdsJson { get; private set; } = "[]";
    public IReadOnlyList<Guid> ParticipantUserIds => JsonSerializer.Deserialize<List<Guid>>(ParticipantUserIdsJson, JsonSerializationDefaults.CreateWeb()) ?? [];
    public string? ParticipantNames { get; private set; }
    public string VisibilityOrganizationIdsJson { get; private set; } = "[]";
    public IReadOnlyList<Guid> VisibilityOrganizationIds => ParseIds(VisibilityOrganizationIdsJson);
    public string VisibilityRoleIdsJson { get; private set; } = "[]";
    public IReadOnlyList<Guid> VisibilityRoleIds => ParseIds(VisibilityRoleIdsJson);
    public PmpProjectWorkItemPriority Priority { get; private set; }
    public PmpProjectWorkItemStatus Status { get; private set; }
    public DateTime? PlannedStartAt { get; private set; }
    public DateTime? PlannedEndAt { get; private set; }
    public DateTime? ReminderAt { get; private set; }
    public DateTime? ActualStartAt { get; private set; }
    public DateTime? ActualEndAt { get; private set; }
    public string? Feedback { get; private set; }
    public string? CompletionRejectionReason { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public PmpProjectWorkItem(Guid projectId, Guid? parentId, string? sourceType, Guid? sourceId, string title, string? description, string? ownerName, string? participantNames, PmpProjectWorkItemPriority priority, DateTime? plannedStartAt, DateTime? plannedEndAt, string? otherInfo, Guid? ownerUserId = null, DateTime? reminderAt = null, IReadOnlyCollection<Guid>? participantUserIds = null, IReadOnlyCollection<Guid>? visibilityOrganizationIds = null, IReadOnlyCollection<Guid>? visibilityRoleIds = null)
    {
        Edit(projectId, parentId, sourceType, sourceId, title, description, ownerName, participantNames, priority, plannedStartAt, plannedEndAt, otherInfo, ownerUserId, reminderAt, participantUserIds, visibilityOrganizationIds, visibilityRoleIds);
        Status = PmpProjectWorkItemStatus.Draft;
    }

    public static PmpProjectWorkItem Restore(Guid id, Guid projectId, Guid? parentId, string? sourceType, Guid? sourceId, string title, string? description, string? ownerName, string? participantNames, PmpProjectWorkItemPriority priority, PmpProjectWorkItemStatus status, DateTime? plannedStartAt, DateTime? plannedEndAt, DateTime? actualStartAt, DateTime? actualEndAt, string? feedback, string? otherInfo, Guid? ownerUserId = null, DateTime? reminderAt = null, string? completionRejectionReason = null, string? participantUserIdsJson = null, string? visibilityOrganizationIdsJson = null, string? visibilityRoleIdsJson = null)
        => new(projectId, parentId, sourceType, sourceId, title, description, ownerName, participantNames, priority, plannedStartAt, plannedEndAt, otherInfo, ownerUserId, reminderAt, ParseIds(participantUserIdsJson), ParseIds(visibilityOrganizationIdsJson), ParseIds(visibilityRoleIdsJson)) { Id = id, Status = status, ActualStartAt = actualStartAt, ActualEndAt = actualEndAt, Feedback = Clean(feedback), CompletionRejectionReason = Clean(completionRejectionReason) };

    public void Edit(Guid projectId, Guid? parentId, string? sourceType, Guid? sourceId, string title, string? description, string? ownerName, string? participantNames, PmpProjectWorkItemPriority priority, DateTime? plannedStartAt, DateTime? plannedEndAt, string? otherInfo, Guid? ownerUserId = null, DateTime? reminderAt = null, IReadOnlyCollection<Guid>? participantUserIds = null, IReadOnlyCollection<Guid>? visibilityOrganizationIds = null, IReadOnlyCollection<Guid>? visibilityRoleIds = null)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("必须关联项目。", nameof(projectId));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("工作项标题不能为空。", nameof(title));
        if (plannedEndAt is DateTime end && plannedStartAt is DateTime start && end < start) throw new ArgumentException("计划结束时间不能早于计划开始时间。", nameof(plannedEndAt));
        if (sourceId is Guid && string.IsNullOrWhiteSpace(sourceType)) throw new ArgumentException("来源标识必须同时提供来源类型。", nameof(sourceType));
        ProjectId = projectId; ParentId = parentId; SourceType = Clean(sourceType); SourceId = sourceId; Title = title.Trim(); Description = Clean(description); OwnerUserId = ownerUserId; OwnerName = Clean(ownerName); ParticipantUserIdsJson = SerializeIds(participantUserIds); ParticipantNames = Clean(participantNames); VisibilityOrganizationIdsJson = SerializeIds(visibilityOrganizationIds); VisibilityRoleIdsJson = SerializeIds(visibilityRoleIds); Priority = priority; PlannedStartAt = plannedStartAt; PlannedEndAt = plannedEndAt; ReminderAt = reminderAt; OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public void SetStatus(PmpProjectWorkItemStatus status, string? feedback, DateTime occurredAt)
    {
        if (status == Status) return;
        var allowed = (Status, status) switch
        {
            (PmpProjectWorkItemStatus.Draft, PmpProjectWorkItemStatus.Open) => true,
            (PmpProjectWorkItemStatus.Draft, PmpProjectWorkItemStatus.Cancelled) => true,
            (PmpProjectWorkItemStatus.Open, PmpProjectWorkItemStatus.InProgress) => true,
            (PmpProjectWorkItemStatus.Open, PmpProjectWorkItemStatus.Cancelled) => true,
            (PmpProjectWorkItemStatus.InProgress, PmpProjectWorkItemStatus.PendingApproval) => true,
            (PmpProjectWorkItemStatus.InProgress, PmpProjectWorkItemStatus.Cancelled) => true,
            _ => false
        };
        if (!allowed) throw new InvalidOperationException($"工作项不能从“{Status}”变更为“{status}”。");
        if (status == PmpProjectWorkItemStatus.PendingApproval && string.IsNullOrWhiteSpace(feedback)) throw new ArgumentException("提交验收必须填写反馈。", nameof(feedback));
        if (status == PmpProjectWorkItemStatus.InProgress) ActualStartAt ??= occurredAt;
        if (status == PmpProjectWorkItemStatus.PendingApproval) CompletionRejectionReason = null;
        if (status == PmpProjectWorkItemStatus.Cancelled) ActualEndAt = occurredAt;
        Feedback = Clean(feedback) ?? Feedback;
        Status = status;
    }

    public void ApproveCompletion(DateTime occurredAt)
    {
        if (Status == PmpProjectWorkItemStatus.Completed) return;
        if (Status != PmpProjectWorkItemStatus.PendingApproval) throw new InvalidOperationException("只有验收审批中的工作项可以完成。");
        ActualEndAt = occurredAt;
        CompletionRejectionReason = null;
        Status = PmpProjectWorkItemStatus.Completed;
    }

    public void RejectCompletion(string? reason)
    {
        if (Status == PmpProjectWorkItemStatus.InProgress) return;
        if (Status != PmpProjectWorkItemStatus.PendingApproval) throw new InvalidOperationException("只有验收审批中的工作项可以驳回。");
        CompletionRejectionReason = Clean(reason);
        Status = PmpProjectWorkItemStatus.InProgress;
    }
    private static string SerializeIds(IReadOnlyCollection<Guid>? ids)
        => JsonSerializer.Serialize((ids ?? []).Where(x => x != Guid.Empty).Distinct().Order().ToArray(), JsonSerializationDefaults.CreateWeb());
    private static IReadOnlyList<Guid> ParseIds(string? json)
    {
        try { return JsonSerializer.Deserialize<List<Guid>>(json ?? "[]", JsonSerializationDefaults.CreateWeb())?.Where(x => x != Guid.Empty).Distinct().ToArray() ?? []; }
        catch (JsonException) { return []; }
    }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
