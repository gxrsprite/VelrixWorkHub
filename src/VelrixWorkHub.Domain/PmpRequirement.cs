namespace VelrixWorkHub.Domain;

public enum PmpRequirementPriority { Low, Medium, High, Critical }
public enum PmpRequirementStatus { Draft, Submitted, Planned, InProgress, Completed, Rejected, Closed }
public enum PmpRequirementType { Functional, NonFunctional, Change, Defect, Other }

public sealed class PmpRequirement
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ProjectId { get; private set; }
    public Guid? ProductId { get; private set; }
    public Guid? BaselineId { get; private set; }
    public string RequirementNo { get; private set; } = string.Empty;
    public bool IsHighlighted { get; private set; }
    public string Proposer { get; private set; } = string.Empty;
    public string? OwnerName { get; private set; }
    public PmpRequirementPriority Priority { get; private set; }
    public PmpRequirementStatus Status { get; private set; }
    public PmpRequirementType RequirementType { get; private set; }
    public DateOnly ProposedDate { get; private set; }
    public DateOnly? DesiredCompletionDate { get; private set; }
    public DateOnly? PlannedCompletionDate { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string? BackgroundValue { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public PmpRequirement(Guid projectId, Guid? productId, Guid? baselineId, string requirementNo, bool isHighlighted, string proposer, PmpRequirementPriority priority, PmpRequirementType requirementType, DateOnly proposedDate, DateOnly? desiredCompletionDate, DateOnly? plannedCompletionDate, string description, string? backgroundValue, string? ownerName, string? otherInfo)
    {
        Edit(projectId, productId, baselineId, requirementNo, isHighlighted, proposer, priority, requirementType, proposedDate, desiredCompletionDate, plannedCompletionDate, description, backgroundValue, ownerName, otherInfo);
        Status = PmpRequirementStatus.Draft;
    }

    public static PmpRequirement Restore(Guid id, Guid projectId, Guid? productId, Guid? baselineId, string requirementNo, bool isHighlighted, string proposer, PmpRequirementPriority priority, PmpRequirementStatus status, PmpRequirementType requirementType, DateOnly proposedDate, DateOnly? desiredCompletionDate, DateOnly? plannedCompletionDate, string description, string? backgroundValue, string? ownerName, string? otherInfo)
        => new(projectId, productId, baselineId, requirementNo, isHighlighted, proposer, priority, requirementType, proposedDate, desiredCompletionDate, plannedCompletionDate, description, backgroundValue, ownerName, otherInfo) { Id = id, Status = status };

    public void Edit(Guid projectId, Guid? productId, Guid? baselineId, string requirementNo, bool isHighlighted, string proposer, PmpRequirementPriority priority, PmpRequirementType requirementType, DateOnly proposedDate, DateOnly? desiredCompletionDate, DateOnly? plannedCompletionDate, string description, string? backgroundValue, string? ownerName, string? otherInfo)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("必须关联项目。", nameof(projectId));
        if (string.IsNullOrWhiteSpace(requirementNo)) throw new ArgumentException("需求编号不能为空。", nameof(requirementNo));
        if (string.IsNullOrWhiteSpace(proposer)) throw new ArgumentException("需求提出人不能为空。", nameof(proposer));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("需求描述不能为空。", nameof(description));
        if (desiredCompletionDate is DateOnly desired && desired < proposedDate) throw new ArgumentException("希望完成日期不能早于提出日期。", nameof(desiredCompletionDate));
        if (plannedCompletionDate is DateOnly planned && planned < proposedDate) throw new ArgumentException("计划完成日期不能早于提出日期。", nameof(plannedCompletionDate));
        ProjectId = projectId; ProductId = productId; BaselineId = baselineId; RequirementNo = requirementNo.Trim(); IsHighlighted = isHighlighted; Proposer = proposer.Trim(); Priority = priority; RequirementType = requirementType; ProposedDate = proposedDate; DesiredCompletionDate = desiredCompletionDate; PlannedCompletionDate = plannedCompletionDate; Description = description.Trim(); BackgroundValue = Clean(backgroundValue); OwnerName = Clean(ownerName); OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public void SetStatus(PmpRequirementStatus status)
    {
        if (status == Status) return;
        var allowed = (Status, status) switch
        {
            (PmpRequirementStatus.Draft, PmpRequirementStatus.Submitted) => true,
            (PmpRequirementStatus.Draft, PmpRequirementStatus.Rejected) => true,
            (PmpRequirementStatus.Submitted, PmpRequirementStatus.Planned) => true,
            (PmpRequirementStatus.Submitted, PmpRequirementStatus.Rejected) => true,
            (PmpRequirementStatus.Rejected, PmpRequirementStatus.Draft) => true,
            (PmpRequirementStatus.Planned, PmpRequirementStatus.InProgress) => true,
            (PmpRequirementStatus.InProgress, PmpRequirementStatus.Completed) => true,
            (PmpRequirementStatus.Completed, PmpRequirementStatus.Closed) => true,
            _ => false
        };
        if (!allowed) throw new InvalidOperationException($"需求不能从“{Status}”变更为“{status}”。");
        Status = status;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
