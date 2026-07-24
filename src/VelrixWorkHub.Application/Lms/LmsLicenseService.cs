using VelrixWorkHub.Domain;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Application.Customers;
using VelrixWorkHub.Application.Contacts;
using System.Text.Json;
namespace VelrixWorkHub.Application.Lms;
public sealed class LmsLicenseService(
    ILmsLicenseRepository repository,
    WorkflowBindingService? bindings = null,
    IWorkflowTransactionBoundary? transactions = null,
    LmsLicenseProductService? products = null,
    CustomerService? customers = null,
    LmsCustomerMachineService? machines = null,
    LmsMachineFeatureService? machineFeatures = null,
    LmsFeatureVersionService? featureVersions = null,
    CustomerContactService? contacts = null,
    NotificationService? notifications = null,
    LmsLicenseAccessService? access = null)
{
    public IReadOnlyList<LmsLicenseRequest> ListRequests(string? applicant = null) => repository.ListRequests().Where(x => string.IsNullOrWhiteSpace(applicant) || x.Applicant.Equals(applicant.Trim(), StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.CreatedAt).ToArray();
    public IReadOnlyList<LmsLicenseAuthorization> ListAuthorizations(bool includeInactive = true, DateTime? now = null)
    {
        var currentTime = now ?? DateTime.Now;
        return repository.ListAuthorizations().Where(x => includeInactive || x.GetEffectiveStatus(currentTime) == LmsLicenseStatus.Active).OrderByDescending(x => x.CreatedAt).ToArray();
    }
    public LmsLicenseRequest CreateRequest(string requestNo, string applicant, string actor, string productName, string? customerName, string featuresJson, DateTime? expiresAt, string? otherInfo, bool isAdministrator = false)
    {
        EnsureApplicantCanCreate(applicant, actor, isAdministrator);
        EnsureFutureExpiry(expiresAt, "申请到期时间");
        products?.EnsureActiveProductName(productName);
        if (repository.ListRequests().Any(x => x.RequestNo.Equals(requestNo.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("许可证申请编号已存在。");
        var item = new LmsLicenseRequest(requestNo, applicant, productName, customerName, featuresJson, expiresAt, otherInfo, DateTime.Now);
        repository.Add(item);
        return item;
    }
    public LmsLicenseRequest CreateMachineRequest(string requestNo, string applicant, string actor, Guid customerId, Guid? contactId, Guid customerMachineId, string productName, string featureVersionIdsJson, DateTime? expiresAt, string? otherInfo, int gracePeriodDays = 0, bool isAdministrator = false)
    {
        EnsureApplicantCanCreate(applicant, actor, isAdministrator);
        EnsureFutureExpiry(expiresAt, "申请到期时间");
        if (customers is null || machines is null || machineFeatures is null || featureVersions is null) throw new InvalidOperationException("许可证申请主数据服务未配置。");
        products?.EnsureActiveProductName(productName);
        if (repository.ListRequests().Any(x => x.RequestNo.Equals(requestNo.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("许可证申请编号已存在。");
        var customer = customers.List().SingleOrDefault(x => x.Id == customerId) ?? throw new InvalidOperationException("CRM 客户不存在。");
        if (customer.Status != CustomerStatus.Active) throw new InvalidOperationException("停用的 CRM 客户不能新建许可证申请。");
        if (contactId is not null)
        {
            if (contacts is null) throw new InvalidOperationException("CRM 联系人服务未配置。");
            var contact = contacts.List(customerId: customerId).SingleOrDefault(x => x.Id == contactId) ?? throw new InvalidOperationException("CRM 联系人不存在或不属于当前客户。");
        }
        var machine = machines.List().SingleOrDefault(x => x.Id == customerMachineId) ?? throw new InvalidOperationException("客户机台不存在。");
        if (machine.Status != LmsCustomerMachineStatus.Active || machine.CustomerId != customerId) throw new InvalidOperationException("客户机台不可用或不属于该 CRM 客户。");
        if (!machine.ProductName.Equals(productName.Trim(), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("申请产品必须与客户机台的许可证产品一致。");
        var selectedIds = ParseFeatureVersionIds(featureVersionIdsJson);
        if (selectedIds.Count == 0) throw new InvalidOperationException("机台许可证申请至少选择一个特性版本。");
        var activeMachineVersionIds = machineFeatures.List(machine.Id, includeDisabled: false).Select(x => x.FeatureVersionId).ToHashSet();
        if (selectedIds.Any(x => !activeMachineVersionIds.Contains(x))) throw new InvalidOperationException("申请只能选择该机台已启用的特性版本。");
        var versions = featureVersions.List();
        var selectedVersions = selectedIds.Select(id => versions.SingleOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("许可证特性版本不存在。")).ToArray();
        if (selectedVersions.Any(x => x.Status != LmsFeatureVersionStatus.Active || x.Scope != LmsFeatureScope.Machine)) throw new InvalidOperationException("申请包含不可用的机台特性版本。");
        EnsureNoActiveAuthorizationConflict(customerMachineId, productName, selectedIds, DateTime.Now);
        var featuresJson = JsonSerializer.Serialize(selectedVersions.Select(x => x.FeatureId.ToString()).ToArray(), JsonSerializationDefaults.CreateWeb());
        var item = new LmsLicenseRequest(requestNo, applicant, productName, customer.Name, featuresJson, expiresAt, otherInfo, DateTime.Now, customerId, customerMachineId, JsonSerializer.Serialize(selectedIds, JsonSerializationDefaults.CreateWeb()), contactId, machine.Model, machine.Environment, gracePeriodDays);
        repository.Add(item);
        return item;
    }
    public void Submit(LmsLicenseRequest item, string actor, bool isAdministrator = false)
    {
        EnsureApplicantCanAct(item, actor, isAdministrator, "提交许可证申请");
        item.Submit();
        repository.Update(item);
    }
    public void DeleteDraft(LmsLicenseRequest item, string actor, bool isAdministrator = false)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureApplicantCanAct(item, actor, isAdministrator, "删除许可证申请草稿");
        if (item.Status != LmsLicenseRequestStatus.Draft) throw new InvalidOperationException("只有草稿许可证申请可以删除。");
        if (repository.ListRequests().All(x => x.Id != item.Id)) throw new InvalidOperationException("许可证申请不存在。");
        repository.RemoveRequest(item.Id);
    }
    public void Cancel(LmsLicenseRequest item, string actor, string? reason = null, bool isAdministrator = false)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("取消操作者不能为空。", nameof(actor));
        if (!isAdministrator && !item.Applicant.Equals(actor.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("只有申请人本人或管理员可以取消许可证申请。");
        if (repository.ListRequests().All(x => x.Id != item.Id)) throw new InvalidOperationException("许可证申请不存在。");
        var running = item.Status == LmsLicenseRequestStatus.Submitted && bindings is not null
            ? bindings.List(nameof(LmsLicenseRequest), item.Id).SingleOrDefault(x => x.Status == WorkflowInstanceStatus.Running)
            : null;
        var previousStatus = item.Status;
        void CancelCore()
        {
            if (running is not null) bindings!.Withdraw(running.Id, actor, reason);
            item.Cancel();
            repository.Update(item);
            notifications?.Publish(
                item.Applicant,
                WorkNotificationKind.Approval,
                "许可证申请已取消",
                $"许可证申请 {item.RequestNo}（{item.ProductName}）已由 {actor.Trim()} 取消。{(string.IsNullOrWhiteSpace(reason) ? string.Empty : $" 原因：{reason.Trim()}")}",
                $"/Lms/License?requestId={item.Id}",
                $"lms-license-request:{item.Id}:cancelled");
        }
        if (transactions is null) CancelCore();
        else transactions.Execute(CancelCore, _ => item.SetStatus(previousStatus));
    }
    public void SubmitAndStartWorkflow(LmsLicenseRequest item, string startedBy, bool isAdministrator = false)
    {
        EnsureApplicantCanAct(item, startedBy, isAdministrator, "提交许可证申请");
        if (bindings is null) throw new InvalidOperationException("许可证申请审批服务未配置。");
        var previousStatus = item.Status;
        WorkflowInstance? workflow = null;
        void SubmitCore()
        {
            item.Submit();
            repository.Update(item);
            workflow = bindings.StartOrGet(WorkflowBindingCodes.LmsLicenseApproval, nameof(LmsLicenseRequest), item.Id, startedBy: startedBy);
        }
        if (transactions is null) SubmitCore();
        else transactions.Execute(SubmitCore, _ => item.SetStatus(previousStatus));
        PublishSubmittedNotification(item, workflow);
    }
    public void ResubmitAfterWithdrawal(LmsLicenseRequest item, string startedBy, bool isAdministrator = false)
    {
        EnsureApplicantCanAct(item, startedBy, isAdministrator, "重新提交许可证申请");
        if (bindings is null) throw new InvalidOperationException("许可证申请审批服务未配置。");
        if (item.Status != LmsLicenseRequestStatus.Submitted) throw new InvalidOperationException("当前许可证申请不能重新提交。");
        var latest = bindings.List(nameof(LmsLicenseRequest), item.Id).OrderByDescending(x => x.StartedAt).FirstOrDefault();
        if (latest?.Status != WorkflowInstanceStatus.Cancelled) throw new InvalidOperationException("只有已撤回的许可证申请可以重新提交。");
        var previousStatus = item.Status;
        WorkflowInstance? workflow = null;
        void ResubmitCore()
        {
            item.SetStatus(LmsLicenseRequestStatus.Withdrawn);
            repository.Update(item);
            workflow = bindings.Resubmit(WorkflowBindingCodes.LmsLicenseApproval, nameof(LmsLicenseRequest), item.Id, startedBy: startedBy);
            item.Submit();
            repository.Update(item);
        }
        if (transactions is null) ResubmitCore();
        else transactions.Execute(ResubmitCore, _ => item.SetStatus(previousStatus));
        PublishSubmittedNotification(item, workflow);
    }
    public LmsLicenseAuthorization RegisterExternalLicense(Guid? requestId, string licenseNo, string externalLicense, string productName, string featuresJson, DateTime? expiresAt, string? otherInfo, string actor, bool isAdministrator = false)
    {
        EnsureActor(actor, "登记外部授权");
        EnsureFutureExpiry(expiresAt, "授权到期时间");
        if (requestId is Guid id)
        {
            var request = repository.ListRequests().FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("关联的许可证申请不存在。");
            EnsureRequestAccess(request, actor, isAdministrator);
            if (request.Status != LmsLicenseRequestStatus.Approved) throw new InvalidOperationException("只有已审批通过的许可证申请可以登记外部 License。");
            if (!request.ProductName.Equals(productName.Trim(), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("外部 License 的产品必须与申请一致。");
        }
        if (repository.ListAuthorizations().Any(x => x.LicenseNo.Equals(licenseNo.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("授权编号已存在。");
        var item = new LmsLicenseAuthorization(requestId, licenseNo, externalLicense, productName, featuresJson, expiresAt, otherInfo, DateTime.Now); repository.Add(item); return item;
    }
    public LmsLicenseAuthorization RegisterExternalLicenseFromRequest(Guid requestId, string licenseNo, string externalLicense, DateTime? expiresAt, string? otherInfo, string actor, bool isAdministrator = false)
    {
        EnsureActor(actor, "登记外部授权");
        EnsureFutureExpiry(expiresAt, "授权到期时间");
        var request = repository.ListRequests().FirstOrDefault(x => x.Id == requestId) ?? throw new InvalidOperationException("关联的许可证申请不存在。");
        EnsureRequestAccess(request, actor, isAdministrator);
        if (request.Status != LmsLicenseRequestStatus.Approved) throw new InvalidOperationException("只有已审批通过的许可证申请可以登记外部 License。");
        if (repository.ListAuthorizations().Any(x => x.LicenseNo.Equals(licenseNo.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("授权编号已存在。");
        if (request.CustomerMachineId is Guid machineId && (expiresAt is null || expiresAt >= DateTime.Now)) EnsureNoActiveAuthorizationConflict(machineId, request.ProductName, ParseFeatureVersionIds(request.FeatureVersionIdsJson), DateTime.Now);
        var item = new LmsLicenseAuthorization(request.Id, licenseNo, externalLicense, request.ProductName, request.FeaturesJson, expiresAt, otherInfo, DateTime.Now, request.CustomerId, request.CustomerMachineId, request.FeatureVersionIdsJson, request.ContactId, model: request.Model, environment: request.Environment, gracePeriodDays: request.GracePeriodDays);
        repository.Add(item);
        return item;
    }
    public LmsLicenseAuthorization ReplaceAuthorization(LmsLicenseAuthorization original, LmsLicenseReplacementKind kind, string licenseNo, string externalLicense, DateTime? expiresAt, string? otherInfo, string actor, string reason, Guid? replacementRequestId = null)
    {
        if (kind == LmsLicenseReplacementKind.MachineChange) throw new InvalidOperationException("换机必须通过 ChangeMachine 指定目标机台。");
        if (original.Status != LmsLicenseStatus.Active) throw new InvalidOperationException("只有有效授权可以续期或重发。");
        if (expiresAt is DateTime expires && expires <= DateTime.Now) throw new InvalidOperationException("续期或重发授权的到期时间必须晚于当前时间。");
        if (repository.ListAuthorizations().Any(x => x.LicenseNo.Equals(licenseNo.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("授权编号已存在。");
        var previousStatus = original.Status;
        LmsLicenseAuthorization? replacement = null;
        void ReplaceCore()
        {
            original.Disable(actor, reason);
            repository.Update(original);
            repository.Add(new LmsLicenseLifecycleEntry(original.Id, LmsLicenseLifecycleAction.Disabled, previousStatus, original.Status, actor, reason, DateTime.Now));
            replacement = new LmsLicenseAuthorization(original.RequestId, licenseNo, externalLicense, original.ProductName, original.FeaturesJson, expiresAt, otherInfo, DateTime.Now, original.CustomerId, original.CustomerMachineId, original.FeatureVersionIdsJson, original.ContactId, original.Id, kind, replacementRequestId, original.Model, original.Environment, original.GracePeriodDays);
            repository.Add(replacement);
        }
        if (transactions is null) ReplaceCore(); else transactions.Execute(ReplaceCore, _ => original.SetStatus(previousStatus));
        return replacement!;
    }
    public LmsLicenseAuthorization ChangeMachine(LmsLicenseAuthorization original, Guid targetMachineId, string licenseNo, string externalLicense, DateTime? expiresAt, string? otherInfo, string actor, string reason, Guid? replacementRequestId = null)
    {
        if (machines is null) throw new InvalidOperationException("客户机台服务未配置。");
        if (original.CustomerId is not Guid customerId) throw new InvalidOperationException("旧授权没有 CRM 客户引用，不能执行换机。");
        if (original.CustomerMachineId == targetMachineId) throw new InvalidOperationException("目标机台必须不同于原机台。");
        var target = machines.List().SingleOrDefault(x => x.Id == targetMachineId) ?? throw new InvalidOperationException("目标客户机台不存在。");
        if (target.Status != LmsCustomerMachineStatus.Active || target.CustomerId != customerId) throw new InvalidOperationException("目标机台未启用或不属于原客户。");
        if (!target.ProductName.Equals(original.ProductName, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("目标机台的许可证产品必须与旧授权一致。");
        if (expiresAt is DateTime expires && expires <= DateTime.Now) throw new InvalidOperationException("换机授权的到期时间必须晚于当前时间。");
        if (original.Status != LmsLicenseStatus.Active) throw new InvalidOperationException("只有有效授权可以换机。");
        if (repository.ListAuthorizations().Any(x => x.LicenseNo.Equals(licenseNo.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("授权编号已存在。");
        EnsureNoActiveAuthorizationConflict(targetMachineId, original.ProductName, ParseFeatureVersionIds(original.FeatureVersionIdsJson), DateTime.Now);
        var previous = original.Status; LmsLicenseAuthorization? replacement = null;
        void Core(){ original.Disable(actor,reason); repository.Update(original); repository.Add(new LmsLicenseLifecycleEntry(original.Id,LmsLicenseLifecycleAction.Disabled,previous,original.Status,actor,reason,DateTime.Now)); replacement=new LmsLicenseAuthorization(original.RequestId,licenseNo,externalLicense,original.ProductName,original.FeaturesJson,expiresAt,otherInfo,DateTime.Now,original.CustomerId,targetMachineId,original.FeatureVersionIdsJson,original.ContactId,original.Id,LmsLicenseReplacementKind.MachineChange,replacementRequestId,target.Model,target.Environment,original.GracePeriodDays); repository.Add(replacement); }
        if(transactions is null)Core();else transactions.Execute(Core,_=>original.SetStatus(previous)); return replacement!;
    }
    public IReadOnlyList<LmsLicenseLifecycleEntry> ListLifecycle(Guid authorizationId) => repository.ListLifecycleEntries(authorizationId).OrderByDescending(x => x.OccurredAt).ToArray();
    public void DisableAuthorization(LmsLicenseAuthorization item, string actor, string reason, DateTime? occurredAt = null, bool isAdministrator = false) => ChangeAuthorization(item, LmsLicenseLifecycleAction.Disabled, actor, reason, occurredAt ?? DateTime.Now, isAdministrator);
    public void EnableAuthorization(LmsLicenseAuthorization item, string actor, string reason, DateTime? occurredAt = null, bool isAdministrator = false) => ChangeAuthorization(item, LmsLicenseLifecycleAction.Enabled, actor, reason, occurredAt ?? DateTime.Now, isAdministrator);
    public void RevokeAuthorization(LmsLicenseAuthorization item, string actor, string reason, DateTime? occurredAt = null, bool isAdministrator = false) => ChangeAuthorization(item, LmsLicenseLifecycleAction.Revoked, actor, reason, occurredAt ?? DateTime.Now, isAdministrator);
    private void ChangeAuthorization(LmsLicenseAuthorization item, LmsLicenseLifecycleAction action, string actor, string reason, DateTime occurredAt, bool isAdministrator)
    {
        EnsureAuthorizationAccess(item, actor, isAdministrator);
        var previousStatus = item.Status;
        void ChangeCore()
        {
            switch (action)
            {
                case LmsLicenseLifecycleAction.Disabled: item.Disable(actor, reason); break;
                case LmsLicenseLifecycleAction.Enabled: item.Enable(actor, reason, occurredAt); break;
                case LmsLicenseLifecycleAction.Revoked: item.Revoke(actor, reason); break;
                default: throw new ArgumentOutOfRangeException(nameof(action));
            }
            repository.Update(item);
            repository.Add(new LmsLicenseLifecycleEntry(item.Id, action, previousStatus, item.Status, actor, reason, occurredAt));
        }
        if (transactions is null) ChangeCore();
        else transactions.Execute(ChangeCore, _ => item.SetStatus(previousStatus));
    }

    private static IReadOnlyList<Guid> ParseFeatureVersionIds(string? value)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(value) ? "[]" : value);
            if (document.RootElement.ValueKind != JsonValueKind.Array) throw new ArgumentException("特性版本必须是 JSON 数组。", nameof(value));
            var ids = document.RootElement.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String && Guid.TryParse(x.GetString(), out var id) ? id : throw new ArgumentException("特性版本数组必须包含 GUID 字符串。", nameof(value))).ToArray();
            if (ids.Distinct().Count() != ids.Length) throw new ArgumentException("特性版本不能重复。", nameof(value));
            return ids;
        }
        catch (JsonException exception) { throw new ArgumentException("特性版本必须是有效 JSON。", nameof(value), exception); }
    }

    private void EnsureNoActiveAuthorizationConflict(Guid customerMachineId, string productName, IReadOnlyList<Guid> featureVersionIds, DateTime now)
    {
        var selected = featureVersionIds.ToHashSet();
        if (selected.Count == 0) return;
        var conflict = repository.ListAuthorizations().FirstOrDefault(authorization =>
            authorization.CustomerMachineId == customerMachineId
            && authorization.ProductName.Equals(productName.Trim(), StringComparison.OrdinalIgnoreCase)
            && authorization.GetEffectiveStatus(now) == LmsLicenseStatus.Active
            && ParseFeatureVersionIds(authorization.FeatureVersionIdsJson).Any(selected.Contains));
        if (conflict is not null) throw new InvalidOperationException("该机台已有覆盖所选特性版本的有效许可证授权。");
    }

    private static void EnsureFutureExpiry(DateTime? expiresAt, string fieldName)
    {
        if (expiresAt is DateTime value && value <= DateTime.Now)
            throw new InvalidOperationException($"{fieldName}必须晚于当前时间。");
    }

    private static void EnsureApplicantCanAct(LmsLicenseRequest item, string actor, bool isAdministrator, string action)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException($"{action}操作者不能为空。", nameof(actor));
        if (!isAdministrator && !item.Applicant.Equals(actor.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"只有申请人本人或管理员可以{action}。");
    }

    private static void EnsureApplicantCanCreate(string applicant, string actor, bool isAdministrator)
    {
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("创建许可证申请操作者不能为空。", nameof(actor));
        if (!isAdministrator && !applicant.Trim().Equals(actor.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("普通用户只能以当前登录用户身份创建许可证申请。");
    }

    private void EnsureRequestAccess(LmsLicenseRequest request, string actor, bool isAdministrator)
    {
        if (isAdministrator) return;
        if (access is not null)
        {
            if (!access.CanReadRequest(request.Id, actor, false))
                throw new InvalidOperationException("当前用户无权为该许可证申请登记外部授权。");
            return;
        }
        if (!request.Applicant.Equals(actor.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("当前用户无权为该许可证申请登记外部授权。");
    }

    private static void EnsureActor(string actor, string action)
    {
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException($"{action}操作者不能为空。", nameof(actor));
    }

    private void EnsureAuthorizationAccess(LmsLicenseAuthorization item, string actor, bool isAdministrator)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureActor(actor, "变更授权生命周期");
        if (repository.ListAuthorizations().All(x => x.Id != item.Id)) throw new InvalidOperationException("授权不存在。");
        if (isAdministrator) return;
        if (item.RequestId is not Guid requestId)
            throw new InvalidOperationException("无关联申请的授权只能由管理员变更生命周期。");
        if (access is not null)
        {
            if (!access.CanReadAuthorization(item.Id, actor, false))
                throw new InvalidOperationException("当前用户无权变更该授权生命周期。");
            return;
        }
        var request = repository.ListRequests().FirstOrDefault(x => x.Id == requestId);
        if (request is null || !request.Applicant.Equals(actor.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("当前用户无权变更该授权生命周期。");
    }

    private void PublishSubmittedNotification(LmsLicenseRequest item, WorkflowInstance? workflow)
    {
        if (notifications is null || workflow is null) return;
        notifications.Publish(
            item.Applicant,
            WorkNotificationKind.Approval,
            "许可证申请已提交审批",
            $"许可证申请 {item.RequestNo}（{item.ProductName}）已提交审批。",
            $"/Lms/License?requestId={item.Id}",
            $"lms-license-request:{item.Id}:submitted:{workflow.Id}",
            workflow.StartedAt);
    }
}
