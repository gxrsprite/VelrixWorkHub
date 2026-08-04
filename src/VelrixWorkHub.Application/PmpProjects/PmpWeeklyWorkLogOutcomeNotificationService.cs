using VelrixWorkHub.Application.Employees;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmpProjects;

/// <summary>向当前受控项目成员投递周工时审批结果；通知失败不影响流程状态回写。</summary>
public sealed class PmpWeeklyWorkLogOutcomeNotificationService(
    IPmpProjectMemberRepository members,
    EmployeeDirectoryService directory,
    NotificationService notifications)
{
    public void Publish(PmpWeeklyWorkLogSubmission item, Guid workflowInstanceId, string target, string? reason = null)
    {
        if (workflowInstanceId == Guid.Empty || target is not ("Approved" or "Rejected")) return;
        var userIds = members.List(item.ProjectId)
            .Where(x => x.MemberName.Equals(item.MemberName, StringComparison.OrdinalIgnoreCase) && x.UserId is Guid)
            .Select(x => x.UserId!.Value)
            .Distinct()
            .ToArray();
        if (userIds.Length != 1) return;
        var userId = userIds[0];
        var recipient = directory.List(status: EmployeeDirectoryStatus.All).FirstOrDefault(x => x.UserId == userId && x.IsEnabled);
        if (recipient is null || string.IsNullOrWhiteSpace(recipient.Username)) return;

        var result = target == "Approved" ? "已批准" : "已驳回";
        var reference = $"{item.WeekStart:yyyy-MM-dd} 当周 · {item.TotalHours:0.##} 小时";
        var content = $"项目周工时 {reference} {result}。";
        if (!string.IsNullOrWhiteSpace(reason)) content += $" 审批意见：{reason.Trim()}";
        notifications.Publish(
            recipient.Username,
            WorkNotificationKind.Approval,
            $"项目周工时{result}",
            content,
            "/Pmp/WorkLog",
            $"pmp-weekly-worklog-outcome:{item.Id}:{workflowInstanceId}:{target}");
    }
}
