using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Lms;

/// <summary>将通用审批结果回写到许可证申请，不参与任何密钥生成或解析。</summary>
public sealed class LmsLicenseWorkflowActionHandler(ILmsLicenseRepository repository, NotificationService? notifications = null) : IWorkflowActionHandler
{
    public bool CanHandle(string businessType) => businessType.Equals(nameof(LmsLicenseRequest), StringComparison.OrdinalIgnoreCase);

    public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
    {
        if (action.Type != WorkflowActionType.SetField || !action.Field.Equals(nameof(LmsLicenseRequest.Status), StringComparison.OrdinalIgnoreCase)
            || !Enum.TryParse<LmsLicenseRequestStatus>(action.Value, out var target))
            throw new InvalidOperationException($"许可证申请流程不支持动作：{action.Type}/{action.Field}/{action.Value}。");

        var item = repository.ListRequests().FirstOrDefault(x => x.Id == context.Instance.BusinessId)
            ?? throw new InvalidOperationException("流程关联的许可证申请不存在或已被删除。");
        if (item.Status == target) return;
        if (target == LmsLicenseRequestStatus.Rejected && string.IsNullOrWhiteSpace(context.Reason))
            throw new InvalidOperationException("驳回许可证申请必须填写审批意见。");

        var allowed = (item.Status, target) switch
        {
            (LmsLicenseRequestStatus.Submitted, LmsLicenseRequestStatus.Approved) => true,
            (LmsLicenseRequestStatus.Submitted, LmsLicenseRequestStatus.Rejected) => true,
            (LmsLicenseRequestStatus.Submitted, LmsLicenseRequestStatus.Withdrawn) => true,
            _ => false
        };
        if (!allowed) throw new InvalidOperationException($"许可证申请不能从“{item.Status}”变更为“{target}”。");
        item.SetStatus(target);
        repository.Update(item);
        PublishResult(item, context, target);
    }

    private void PublishResult(LmsLicenseRequest item, WorkflowActionContext context, LmsLicenseRequestStatus target)
    {
        if (notifications is null || target is not (LmsLicenseRequestStatus.Approved or LmsLicenseRequestStatus.Rejected)) return;
        var result = target == LmsLicenseRequestStatus.Approved ? "已批准" : "已驳回";
        var content = $"许可证申请 {item.RequestNo}（{item.ProductName}）{result}。";
        if (!string.IsNullOrWhiteSpace(context.Reason)) content += $" 审批意见：{context.Reason.Trim()}";
        notifications.Publish(item.Applicant, WorkNotificationKind.Approval, $"许可证申请{result}", content, $"/Lms/License?requestId={item.Id}", $"lms-license-request:{item.Id}:workflow:{context.Instance.Id}:{target}");
    }
}
