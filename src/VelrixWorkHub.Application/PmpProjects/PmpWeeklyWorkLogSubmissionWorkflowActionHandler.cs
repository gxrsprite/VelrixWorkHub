using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.PmpProjects;
public sealed class PmpWeeklyWorkLogSubmissionWorkflowActionHandler(IPmpWeeklyWorkLogSubmissionRepository repository, IPmpWeeklyWorkLogSubmissionWorkflowApprover? approver = null, PmpWeeklyWorkLogOutcomeNotificationService? outcomeNotifications = null) : IWorkflowActionHandler
{
    public bool CanHandle(string businessType) => businessType.Equals(nameof(PmpWeeklyWorkLogSubmission), StringComparison.OrdinalIgnoreCase);
    public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
    {
        if (action.Type != WorkflowActionType.SetField || !action.Field.Equals(nameof(PmpWeeklyWorkLogSubmission.Status), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("工时周报流程动作无效。");
        var item = repository.List().FirstOrDefault(x => x.Id == context.Instance.BusinessId) ?? throw new InvalidOperationException("流程关联的工时周报不存在或已被删除。");
        if (action.Value == nameof(PmpWeeklyWorkLogSubmissionStatus.Approved)) { if (approver is null) { item.Approve(); repository.Update(item); } else approver.ApplyApproval(item); outcomeNotifications?.Publish(item, context.Instance.Id, "Approved"); return; }
        if (action.Value == nameof(PmpWeeklyWorkLogSubmissionStatus.Rejected)) { if (approver is null) { item.Reject(context.Reason); repository.Update(item); } else approver.ApplyRejection(item, context.Reason); outcomeNotifications?.Publish(item, context.Instance.Id, "Rejected", context.Reason); return; }
        if (action.Value == nameof(PmpWeeklyWorkLogSubmissionStatus.Withdrawn)) { if (approver is null) { item.Withdraw(); repository.Update(item); } else approver.ApplyWithdrawal(item); return; }
        throw new InvalidOperationException("工时周报流程状态回写无效。");
    }
}
