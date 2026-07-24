using System.Text.Json;
using VelrixWorkHub.Application.Employees;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.SimpleForms;

/// <summary>印章申请通过后通知表单指定的被申请人；不生成盖章或业务单据。</summary>
public sealed class SealRequestNotificationHandler(NotificationService notifications, EmployeeDirectoryService directory) : ISimpleFormCompletionHandler
{
    public string EventCode => "SEAL_REQUEST_NOTIFY_RECIPIENT";

    public void Handle(SimpleFormCompletionContext context)
    {
        if (context.Status != SimpleFormSubmissionStatus.Approved) return;
        using var document = JsonDocument.Parse(context.DataJson);
        if (!document.RootElement.TryGetProperty("recipient", out var recipient) || recipient.ValueKind != JsonValueKind.Object || !recipient.TryGetProperty("id", out var idValue) || !Guid.TryParse(idValue.GetString(), out var userId))
            throw new InvalidOperationException("印章申请缺少有效的被申请人。");
        var user = directory.List(status: EmployeeDirectoryStatus.All).FirstOrDefault(x => x.UserId == userId && x.IsEnabled)
            ?? throw new InvalidOperationException("印章申请的被申请人不存在或已停用。");
        notifications.Publish(user.Username, WorkNotificationKind.System, "印章申请已批准", $"印章申请 {context.SubmissionId} 已审批通过，请处理后续用印事宜。", "/Workflow/SimpleForm", $"simple-form:seal-request:{context.SubmissionId}:approved:recipient");
    }
}
