using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmpProjects;

/// <summary>将通用 Workflow 不可变操作历史投影为周工时卡片可用的最近审批结果。</summary>
public sealed class PmpWeeklyWorkLogSubmissionWorkflowHistoryService(WorkflowOperationService operations)
{
    public WorkflowOperation? GetLatestDecision(PmpWeeklyWorkLogSubmission item)
        => operations.List(businessType: nameof(PmpWeeklyWorkLogSubmission), businessId: item.Id)
            .Where(x => x.Kind is WorkflowOperationKind.Approved or WorkflowOperationKind.Rejected or WorkflowOperationKind.Returned or WorkflowOperationKind.Transferred or WorkflowOperationKind.Withdrawn)
            .OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
            .FirstOrDefault();
}
