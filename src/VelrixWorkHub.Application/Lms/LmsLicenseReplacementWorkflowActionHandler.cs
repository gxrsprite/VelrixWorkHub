using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Lms;

public sealed class LmsLicenseReplacementWorkflowActionHandler(
    ILmsLicenseReplacementRequestRepository repository,
    IServiceProvider services) : IWorkflowActionHandler
{
    public bool CanHandle(string businessType) => businessType.Equals(nameof(LmsLicenseReplacementRequest), StringComparison.OrdinalIgnoreCase);

    public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
    {
        if (action.Type != WorkflowActionType.SetField || !action.Field.Equals(nameof(LmsLicenseReplacementRequest.Status), StringComparison.OrdinalIgnoreCase)
            || !Enum.TryParse<LmsLicenseReplacementRequestStatus>(action.Value, out var target))
            throw new InvalidOperationException($"授权替代申请流程不支持动作：{action.Type}/{action.Field}/{action.Value}。");

        var item = repository.List().FirstOrDefault(x => x.Id == context.Instance.BusinessId)
            ?? throw new InvalidOperationException("流程关联的授权替代申请不存在或已被删除。");
        if (item.Status == target) return;
        if (item.Status != LmsLicenseReplacementRequestStatus.Submitted)
            throw new InvalidOperationException($"授权替代申请不能从“{item.Status}”变更为“{target}”。");

        switch (target)
        {
            case LmsLicenseReplacementRequestStatus.Approved:
                var actor = context.Actor?.Trim();
                if (string.IsNullOrWhiteSpace(actor)) throw new InvalidOperationException("批准授权替代申请必须提供实际审批人。");
                // LmsLicenseService 会启动替代 Workflow；在动作处理器构造期直接注入会形成
                // WorkflowActionExecutor -> handler -> LmsLicenseService -> WorkflowBindingService 的 DI 环。
                // 动作实际执行时当前 WorkflowActionExecutor 已在作用域内，可安全解析既有实例。
                var licenses = services.GetService(typeof(LmsLicenseService)) as LmsLicenseService
                    ?? throw new InvalidOperationException("授权替代服务未配置。");
                var original = licenses.ListAuthorizations().FirstOrDefault(x => x.Id == item.OriginalAuthorizationId)
                    ?? throw new InvalidOperationException("替代申请关联的原授权不存在或已被删除。");
                if (item.Kind == LmsLicenseReplacementKind.MachineChange)
                    licenses.ChangeMachine(original, item.TargetMachineId!.Value, item.LicenseNo, item.ExternalLicense, item.ExpiresAt, item.OtherInfo, actor, item.Reason, item.Id);
                else
                    licenses.ReplaceAuthorization(original, item.Kind, item.LicenseNo, item.ExternalLicense, item.ExpiresAt, item.OtherInfo, actor, item.Reason, item.Id);
                break;
            case LmsLicenseReplacementRequestStatus.Rejected:
            case LmsLicenseReplacementRequestStatus.Withdrawn:
                break;
            default:
                throw new InvalidOperationException("授权替代申请流程只支持批准、驳回或撤回。");
        }

        item.SetStatus(target);
        repository.Update(item);
    }
}
