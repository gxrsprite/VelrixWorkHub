using VelrixWorkHub.Application.Employees;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Notifications;

/// <summary>将 OA Workflow 的批准/驳回结果投递给申请人；不改变业务状态，也不建立 OA 私有通知表。</summary>
public sealed class OaWorkflowOutcomeNotificationService(
    EmployeeDirectoryService directory,
    NotificationService notifications)
{
    public void Publish(Guid applicantUserId, string businessType, Guid businessId, string subject,
        string reference, string href, Guid workflowInstanceId, string target, string? reason = null)
    {
        if (applicantUserId == Guid.Empty || workflowInstanceId == Guid.Empty) return;
        if (target is not ("Approved" or "Rejected")) return;

        var recipient = directory.List(status: EmployeeDirectoryStatus.All)
            .FirstOrDefault(x => x.UserId == applicantUserId && x.IsEnabled);
        if (recipient is null || string.IsNullOrWhiteSpace(recipient.Username)) return;

        var result = target == "Approved" ? "已批准" : "已驳回";
        var content = $"{subject} {reference} {result}。";
        if (!string.IsNullOrWhiteSpace(reason)) content += $" 审批意见：{reason.Trim()}";
        notifications.Publish(
            recipient.Username,
            WorkNotificationKind.Approval,
            $"{subject}{result}",
            content,
            href,
            $"oa-workflow-outcome:{businessType}:{businessId}:{workflowInstanceId}:{target}");
    }
}
