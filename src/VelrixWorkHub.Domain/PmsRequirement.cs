namespace VelrixWorkHub.Domain;

public enum PmsRequirementPriority { Low, Medium, High, Critical }
public enum PmsRequirementStatus { Draft, Submitted, Planned, InProgress, Completed, Rejected, Closed }
public enum PmsRequirementType { Functional, NonFunctional, Change, Defect, Other }

public sealed class PmsRequirement
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ProjectId { get; private set; }
    public Guid? ProductId { get; private set; }
    public Guid? BaselineId { get; private set; }
    public string RequirementNo { get; private set; } = string.Empty;
    public bool IsHighlighted { get; private set; }
    public string Proposer { get; private set; } = string.Empty;
    public string? OwnerName { get; private set; }
    public PmsRequirementPriority Priority { get; private set; }
    public PmsRequirementStatus Status { get; private set; }
    public PmsRequirementType RequirementType { get; private set; }
    public DateOnly ProposedDate { get; private set; }
    public DateOnly? DesiredCompletionDate { get; private set; }
    public DateOnly? PlannedCompletionDate { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string? BackgroundValue { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public PmsRequirement(Guid projectId, Guid? productId, Guid? baselineId, string requirementNo, bool isHighlighted, string proposer, PmsRequirementPriority priority, PmsRequirementType requirementType, DateOnly proposedDate, DateOnly? desiredCompletionDate, DateOnly? plannedCompletionDate, string description, string? backgroundValue, string? ownerName, string? otherInfo)
    {
        Edit(projectId, productId, baselineId, requirementNo, isHighlighted, proposer, priority, requirementType, proposedDate, desiredCompletionDate, plannedCompletionDate, description, backgroundValue, ownerName, otherInfo);
        Status = PmsRequirementStatus.Draft;
    }

    public static PmsRequirement Restore(Guid id, Guid projectId, Guid? productId, Guid? baselineId, string requirementNo, bool isHighlighted, string proposer, PmsRequirementPriority priority, PmsRequirementStatus status, PmsRequirementType requirementType, DateOnly proposedDate, DateOnly? desiredCompletionDate, DateOnly? plannedCompletionDate, string description, string? backgroundValue, string? ownerName, string? otherInfo)
        => new(projectId, productId, baselineId, requirementNo, isHighlighted, proposer, priority, requirementType, proposedDate, desiredCompletionDate, plannedCompletionDate, description, backgroundValue, ownerName, otherInfo) { Id = id, Status = status };

    public void Edit(Guid projectId, Guid? productId, Guid? baselineId, string requirementNo, bool isHighlighted, string proposer, PmsRequirementPriority priority, PmsRequirementType requirementType, DateOnly proposedDate, DateOnly? desiredCompletionDate, DateOnly? plannedCompletionDate, string description, string? backgroundValue, string? ownerName, string? otherInfo)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("必须关联项目。", nameof(projectId));
        if (string.IsNullOrWhiteSpace(requirementNo)) throw new ArgumentException("需求编号不能为空。", nameof(requirementNo));
        if (string.IsNullOrWhiteSpace(proposer)) throw new ArgumentException("需求提出人不能为空。", nameof(proposer));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("需求描述不能为空。", nameof(description));
        if (desiredCompletionDate is DateOnly desired && desired < proposedDate) throw new ArgumentException("希望完成日期不能早于提出日期。", nameof(desiredCompletionDate));
        if (plannedCompletionDate is DateOnly planned && planned < proposedDate) throw new ArgumentException("计划完成日期不能早于提出日期。", nameof(plannedCompletionDate));
        ProjectId = projectId; ProductId = productId; BaselineId = baselineId; RequirementNo = requirementNo.Trim(); IsHighlighted = isHighlighted; Proposer = proposer.Trim(); Priority = priority; RequirementType = requirementType; ProposedDate = proposedDate; DesiredCompletionDate = desiredCompletionDate; PlannedCompletionDate = plannedCompletionDate; Description = description.Trim(); BackgroundValue = Clean(backgroundValue); OwnerName = Clean(ownerName); OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public void SetStatus(PmsRequirementStatus status)
    {
        if (status == Status) return;
        var allowed = (Status, status) switch
        {
            (PmsRequirementStatus.Draft, PmsRequirementStatus.Submitted) => true,
            (PmsRequirementStatus.Draft, PmsRequirementStatus.Rejected) => true,
            (PmsRequirementStatus.Submitted, PmsRequirementStatus.Planned) => true,
            (PmsRequirementStatus.Submitted, PmsRequirementStatus.Rejected) => true,
            (PmsRequirementStatus.Rejected, PmsRequirementStatus.Draft) => true,
            (PmsRequirementStatus.Planned, PmsRequirementStatus.InProgress) => true,
            (PmsRequirementStatus.InProgress, PmsRequirementStatus.Completed) => true,
            (PmsRequirementStatus.Completed, PmsRequirementStatus.Closed) => true,
            _ => false
        };
        if (!allowed) throw new InvalidOperationException($"需求不能从“{Status}”变更为“{status}”。");
        Status = status;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
