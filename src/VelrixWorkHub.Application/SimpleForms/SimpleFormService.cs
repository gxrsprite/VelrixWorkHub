using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Application.Employees;
using VelrixWorkHub.Domain;
using System.Text.Json;

namespace VelrixWorkHub.Application.SimpleForms;

public interface ISimpleFormDefinitionRepository
{
    IReadOnlyList<SimpleFormDefinition> List();
    SimpleFormDefinition? Get(Guid id);
    void Add(SimpleFormDefinition item);
    void Update(SimpleFormDefinition item);
}

public interface ISimpleFormDefinitionVersionRepository
{
    IReadOnlyList<SimpleFormDefinitionVersion> List(Guid? definitionId = null);
    SimpleFormDefinitionVersion? Get(Guid id);
    void Add(SimpleFormDefinitionVersion item);
    void Update(SimpleFormDefinitionVersion item);
}

public interface ISimpleFormSubmissionRepository
{
    IReadOnlyList<SimpleFormSubmission> List(Guid? applicantUserId = null, Guid? definitionId = null);
    SimpleFormSubmission? Get(Guid id);
    void Add(SimpleFormSubmission item);
    void Update(SimpleFormSubmission item);
}

public interface ISimpleFormWorkflowSnapshotRepository
{
    SimpleFormWorkflowSnapshot? GetByWorkflowInstanceId(Guid workflowInstanceId);
    void Add(SimpleFormWorkflowSnapshot item);
}

public interface ISimpleFormSubmissionWorkflowApprover
{
    void ApplyApproval(SimpleFormSubmission item);
    void ApplyRejection(SimpleFormSubmission item, string? reason);
}

public sealed class SimpleFormService(
    ISimpleFormDefinitionRepository definitions,
    ISimpleFormDefinitionVersionRepository versions,
    ISimpleFormSubmissionRepository submissions,
    WorkflowDefinitionService workflowDefinitions,
    WorkflowBindingService? bindings = null,
    IWorkflowTransactionBoundary? transactions = null,
    IEnumerable<ISimpleFormCompletionHandler>? completionHandlers = null,
    ISimpleFormWorkflowSnapshotRepository? snapshots = null,
    EmployeeDirectoryService? directory = null,
    SimpleFormCompletionOutboxService? completionOutbox = null) : ISimpleFormSubmissionWorkflowApprover
{
    public IReadOnlyList<SimpleFormDefinition> ListDefinitions() => definitions.List().OrderBy(x => x.Code).ToArray();
    public IReadOnlyList<SimpleFormDefinitionVersion> ListVersions(Guid definitionId) => definitionId == Guid.Empty ? [] : versions.List(definitionId).OrderByDescending(x => x.VersionNumber).ToArray();
    public IReadOnlyList<SimpleFormSubmission> ListMine(Guid applicantUserId) => applicantUserId == Guid.Empty ? [] : submissions.List(applicantUserId).OrderByDescending(x => x.CreatedAt).ToArray();
    public SimpleFormSubmission? GetSubmission(Guid id) => submissions.Get(id);

    public SimpleFormDefinition CreateDefinition(string code, string name, string? description, string workflowDefinitionCode, string? completionEventCode, string initialSchemaJson)
    {
        var item = new SimpleFormDefinition(code, name, description, workflowDefinitionCode, completionEventCode, DateTime.Now);
        if (definitions.List().Any(x => x.Code.Equals(item.Code, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("表单编码已存在。");
        EnsurePublishedWorkflow(item.WorkflowDefinitionCode);
        var version = new SimpleFormDefinitionVersion(item.Id, 1, initialSchemaJson, DateTime.Now);
        definitions.Add(item);
        versions.Add(version);
        return item;
    }

    public SimpleFormDefinitionVersion CreateDraftVersion(SimpleFormDefinition definition)
    {
        var latest = versions.List(definition.Id).Select(x => x.VersionNumber).DefaultIfEmpty(0).Max();
        var source = versions.List(definition.Id).FirstOrDefault(x => x.VersionNumber == definition.PublishedVersionNumber)
            ?? throw new InvalidOperationException("当前表单没有可复制的发布版本。");
        var version = new SimpleFormDefinitionVersion(definition.Id, latest + 1, source.SchemaJson, DateTime.Now);
        versions.Add(version);
        return version;
    }

    public void SaveDefinition(SimpleFormDefinition definition, string name, string? description, string workflowDefinitionCode, string? completionEventCode)
    {
        definition.Edit(name, description, workflowDefinitionCode, completionEventCode);
        EnsurePublishedWorkflow(definition.WorkflowDefinitionCode);
        definitions.Update(definition);
    }

    public void SaveDraftVersion(SimpleFormDefinitionVersion version, string schemaJson)
    {
        version.SaveSchema(schemaJson);
        versions.Update(version);
    }

    public void Publish(SimpleFormDefinition definition, SimpleFormDefinitionVersion version)
    {
        if (version.DefinitionId != definition.Id) throw new InvalidOperationException("表单版本不属于该定义。");
        EnsurePublishedWorkflow(definition.WorkflowDefinitionCode);
        version.Publish(DateTime.Now);
        definition.Publish(version.VersionNumber);
        versions.Update(version);
        definitions.Update(definition);
    }

    public SimpleFormSubmission CreateSubmission(Guid definitionId, Guid applicantUserId, string applicantName, string dataJson)
    {
        var definition = definitions.Get(definitionId) ?? throw new InvalidOperationException("表单定义不存在或已删除。");
        var version = versions.List(definition.Id).SingleOrDefault(x => x.VersionNumber == definition.PublishedVersionNumber && x.Status == SimpleFormDefinitionVersionStatus.Published)
            ?? throw new InvalidOperationException("表单尚未发布，不能发起申请。");
        ValidateControlledReferences(version.SchemaJson, dataJson);
        var item = new SimpleFormSubmission(definition.Id, definition.Code, version.VersionNumber, definition.WorkflowDefinitionCode, definition.CompletionEventCode, applicantUserId, applicantName, version.SchemaJson, dataJson, DateTime.Now);
        submissions.Add(item);
        return item;
    }

    public void EditSubmission(SimpleFormSubmission item, Guid actorUserId, string dataJson)
    {
        EnsureApplicant(item, actorUserId);
        ValidateControlledReferences(item.SchemaJson, dataJson);
        item.Edit(dataJson);
        submissions.Update(item);
    }

    public void SubmitAndStartWorkflow(SimpleFormSubmission item, Guid actorUserId, string actor)
    {
        EnsureApplicant(item, actorUserId);
        if (bindings is null) throw new InvalidOperationException("简单表单审批服务未配置。");
        var previousStatus = item.Status;
        void Core()
        {
            item.Submit(DateTime.Now);
            submissions.Update(item);
            var instance = bindings.StartOrGet(item.WorkflowDefinitionCode, nameof(SimpleFormSubmission), item.Id, startedBy: actor);
            EnsureWorkflowSnapshot(instance.Id, item);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => item.SetStatus(previousStatus));
    }

    public void Cancel(SimpleFormSubmission item, Guid actorUserId, string actor)
    {
        EnsureApplicant(item, actorUserId);
        var running = bindings?.List(nameof(SimpleFormSubmission), item.Id).SingleOrDefault(x => x.Status == WorkflowInstanceStatus.Running);
        var previousStatus = item.Status;
        void Core()
        {
            if (running is not null) bindings!.Withdraw(running.Id, actor, "申请人撤回简单表单申请");
            item.Cancel();
            submissions.Update(item);
            DispatchCompletionAfterCommit(item);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => item.SetStatus(previousStatus));
    }

    public void ApplyApproval(SimpleFormSubmission item)
    {
        if (item.Status == SimpleFormSubmissionStatus.Approved) return;
        item.Approve();
        submissions.Update(item);
        DispatchCompletionAfterCommit(item);
    }

    public void ApplyRejection(SimpleFormSubmission item, string? reason)
    {
        if (item.Status == SimpleFormSubmissionStatus.Rejected) return;
        item.Reject(reason);
        submissions.Update(item);
        DispatchCompletionAfterCommit(item);
    }

    public SimpleFormWorkflowSnapshot? GetWorkflowSnapshot(Guid workflowInstanceId) => workflowInstanceId == Guid.Empty ? null : snapshots?.GetByWorkflowInstanceId(workflowInstanceId);

    private void EnsurePublishedWorkflow(string code)
    {
        if (!workflowDefinitions.List(code, WorkflowDefinitionStatus.Published).Any()) throw new InvalidOperationException($"未找到已发布流程：{code}。");
    }
    private static void EnsureApplicant(SimpleFormSubmission item, Guid actorUserId)
    {
        if (actorUserId == Guid.Empty || item.ApplicantUserId != actorUserId) throw new UnauthorizedAccessException("当前用户不能操作其他员工的表单申请。");
    }
    private void RaiseCompletionEvent(SimpleFormSubmission item)
    {
        var context = new SimpleFormCompletionContext(item.Id, item.DefinitionCode, item.CompletionEventCode, item.FormVersionNumber, item.Status, item.SchemaJson, item.DataJson, item.ApplicantUserId, item.ApplicantName);
        if (completionOutbox is not null)
        {
            completionOutbox.Enqueue(context);
            return;
        }
        var handlers = (completionHandlers ?? []).Where(x => x.EventCode.Equals(item.CompletionEventCode, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (handlers.Length != 1) throw new InvalidOperationException($"表单完成事件“{item.CompletionEventCode}”没有唯一处理器。处理器数量：{handlers.Length}。");
        handlers[0].Handle(context);
    }
    private void DispatchCompletionAfterCommit(SimpleFormSubmission item)
    {
        void Dispatch() { RaiseCompletionEvent(item); if (completionOutbox is not null) completionOutbox.DispatchPending(take: 1); }
        if (transactions is null) { Dispatch(); return; }
        transactions.Execute(() => { }, afterRollback: null, afterCommit: Dispatch);
    }
    private void EnsureWorkflowSnapshot(Guid workflowInstanceId, SimpleFormSubmission item)
    {
        if (snapshots is null) return;
        if (snapshots.GetByWorkflowInstanceId(workflowInstanceId) is null)
            snapshots.Add(new SimpleFormWorkflowSnapshot(workflowInstanceId, item, DateTime.Now));
    }
    private void ValidateControlledReferences(string schemaJson, string dataJson)
    {
        if (directory is null) return;
        var schema = SimpleFormSchema.Parse(schemaJson);
        using var document = JsonDocument.Parse(dataJson);
        foreach (var field in schema.Fields.Where(x => x.Control is SimpleFormFieldControl.PersonPicker or SimpleFormFieldControl.DepartmentPicker))
        {
            if (!document.RootElement.TryGetProperty(field.Key, out var value) || value.ValueKind == JsonValueKind.Null) continue;
            if (!value.TryGetProperty("id", out var idValue) || !Guid.TryParse(idValue.GetString(), out var id)) throw new ArgumentException($"字段“{field.Label}”引用无效。", nameof(dataJson));
            var valid = field.Control == SimpleFormFieldControl.PersonPicker
                ? directory.List(status: EmployeeDirectoryStatus.Enabled).Any(x => x.UserId == id)
                : directory.ListOrganizations().Any(x => x.Id == id);
            if (!valid) throw new ArgumentException($"字段“{field.Label}”引用不存在或不可用。", nameof(dataJson));
        }
    }
}
