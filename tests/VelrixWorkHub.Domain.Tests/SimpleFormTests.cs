using System.Text.Json;
using VelrixWorkHub.Application.Employees;
using VelrixWorkHub.Application.Attachments;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Application.SimpleForms;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class SimpleFormTests
{
    [Fact]
    public void Schema_NormalizesUnpairedHalfFieldToFullRow()
    {
        var schema = new SimpleFormSchema("外出登记",
        [
            new SimpleFormFieldSchema("city", "城市", null, SimpleFormFieldControl.Select, SimpleFormFieldWidth.Half, true, [new SimpleFormOption("SH", "上海")]),
            new SimpleFormFieldSchema("reason", "事由", null, SimpleFormFieldControl.MultiLineText, SimpleFormFieldWidth.Full, true),
            new SimpleFormFieldSchema("contact", "联系人", null, SimpleFormFieldControl.Text, SimpleFormFieldWidth.Half, false)
        ]);

        var rows = schema.GetLayoutRows();

        Assert.Equal(3, rows.Count);
        Assert.All(rows, row => Assert.True(row.IsFullWidth));
        Assert.Equal("contact", rows[2].Fields.Single().Key);
    }

    [Fact]
    public void Schema_ValidatesControlsOptionsAndData()
    {
        var schema = new SimpleFormSchema("外出登记",
        [
            new SimpleFormFieldSchema("city", "城市", null, SimpleFormFieldControl.Select, SimpleFormFieldWidth.Half, true, [new SimpleFormOption("SH", "上海")]),
            new SimpleFormFieldSchema("attendees", "参与人员", null, SimpleFormFieldControl.MultiSelect, SimpleFormFieldWidth.Half, false, [new SimpleFormOption("A", "甲"), new SimpleFormOption("B", "乙")]),
            new SimpleFormFieldSchema("person", "联系人", null, SimpleFormFieldControl.PersonPicker, SimpleFormFieldWidth.Full, true, null, "Person")
        ]);
        var schemaJson = JsonSerializer.Serialize(schema, JsonSerializationDefaults.CreateWeb());

        var parsed = SimpleFormSchema.Parse(schemaJson);
        parsed.ValidateData("{\"city\":\"SH\",\"attendees\":[\"A\"],\"person\":{\"id\":\"u1\",\"label\":\"甲\"}}");

        Assert.Throws<ArgumentException>(() => parsed.ValidateData("{\"city\":\"BJ\",\"person\":{\"id\":\"u1\",\"label\":\"甲\"}}"));
        Assert.Throws<ArgumentException>(() => parsed.ValidateData("{\"city\":\"SH\"}"));
    }

    [Fact]
    public void Submission_FreezesSchemaAndAllowsRejectedResubmission()
    {
        const string schema = "{\"title\":\"登记\",\"fields\":[{\"key\":\"reason\",\"label\":\"事由\",\"control\":\"Text\",\"width\":\"Half\",\"required\":true}]}";
        var item = new SimpleFormSubmission(Guid.CreateVersion7(), "OUTING", 1, "OUTING_APPROVAL", "NONE", Guid.CreateVersion7(), "alice", schema, "{\"reason\":\"客户拜访\"}", DateTime.Now);

        item.Submit(DateTime.Now);
        item.Reject("请补充说明");
        item.Edit("{\"reason\":\"客户拜访，已补充地点\"}");
        item.Submit(DateTime.Now);

        Assert.Equal(SimpleFormSubmissionStatus.Submitted, item.Status);
        Assert.Null(item.RejectionReason);
        Assert.Contains("客户拜访", item.DataJson);
    }

    [Fact]
    public void WorkflowSnapshot_PreservesOriginalDataAfterRejectedSubmissionIsEdited()
    {
        const string schema = "{\"title\":\"登记\",\"fields\":[{\"key\":\"reason\",\"label\":\"事由\",\"control\":\"Text\",\"width\":\"Full\",\"required\":true}]}";
        var item = new SimpleFormSubmission(Guid.CreateVersion7(), "OUTING", 1, "OUTING_APPROVAL", "NONE", Guid.CreateVersion7(), "alice", schema, "{\"reason\":\"首次填写\"}", DateTime.Now);
        var snapshot = new SimpleFormWorkflowSnapshot(Guid.CreateVersion7(), item, DateTime.Now);

        item.Submit(DateTime.Now);
        item.Reject("补充说明");
        item.Edit("{\"reason\":\"重新填写\"}");

        Assert.Contains("首次填写", snapshot.DataJson);
        Assert.DoesNotContain("重新填写", snapshot.DataJson);
    }

    [Fact]
    public async Task Attachments_RejectOtherApplicantAndTerminalWrites()
    {
        const string schema = "{\"title\":\"登记\",\"fields\":[{\"key\":\"reason\",\"label\":\"事由\",\"control\":\"Text\",\"width\":\"Full\",\"required\":true}]}";
        var owner = Guid.CreateVersion7();
        var item = new SimpleFormSubmission(Guid.CreateVersion7(), "OUTING", 1, "OUTING_APPROVAL", "NONE", owner, "alice", schema, "{\"reason\":\"测试\"}", DateTime.Now);
        var service = new SimpleFormAttachmentService(new SubmissionRepository(item), new AttachmentService(new AttachmentRepository(), new AttachmentAuditRepository()));

        Assert.Empty(service.List(item.Id, owner));
        Assert.Throws<UnauthorizedAccessException>(() => service.List(item.Id, Guid.CreateVersion7()));
        item.Submit(DateTime.Now);
        item.Approve();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(item.Id, owner, "alice", "a.txt", "text/plain", Stream.Null, null!));
    }

    [Fact]
    public void CompletionOutbox_DeliversAndRetriesFailedHandler()
    {
        var repository = new CompletionEventRepository();
        var handler = new RecordingCompletionHandler(failFirst: true);
        var service = new SimpleFormCompletionOutboxService(repository, [handler]);
        var context = new SimpleFormCompletionContext(Guid.CreateVersion7(), "TEST", "TEST_EVENT", 1, SimpleFormSubmissionStatus.Approved,
            "{\"title\":\"测试\",\"fields\":[{\"key\":\"reason\",\"label\":\"事由\",\"control\":\"Text\",\"width\":\"Full\",\"required\":true}]}", "{\"reason\":\"完成\"}", Guid.CreateVersion7(), "alice");

        service.Enqueue(context);
        Assert.Equal(0, service.DispatchPending());
        Assert.Single(repository.Items, x => x.Status == SimpleFormCompletionEventStatus.Pending && x.RetryCount == 1);

        Assert.Equal(1, service.DispatchPending());
        Assert.Single(repository.Items, x => x.Status == SimpleFormCompletionEventStatus.Delivered);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public void SealRequestApproval_CompletesFormAndNotifiesSelectedRecipientOnce()
    {
        var recipientId = Guid.CreateVersion7();
        var submission = new SimpleFormSubmission(
            Guid.CreateVersion7(), "SIMPLE_SEAL_REQUEST", 1, WorkflowBindingCodes.SimpleSealRequestApproval,
            "SEAL_REQUEST_NOTIFY_RECIPIENT", Guid.CreateVersion7(), "applicant",
            "{\"title\":\"印章申请\",\"fields\":[{\"key\":\"recipient\",\"label\":\"被申请人\",\"control\":\"PersonPicker\",\"width\":\"Full\",\"required\":true,\"source\":\"Person\"}]}",
            $"{{\"recipient\":{{\"id\":\"{recipientId}\",\"label\":\"用印经办人\"}}}}", DateTime.Now);
        submission.Submit(DateTime.Now);
        var submissions = new SubmissionRepository(submission);
        var notifications = new NotificationRepository();
        var directory = new EmployeeDirectoryService(new DirectoryRepository(
            new EmployeeDirectoryEntry(recipientId, "seal.operator", "用印经办人", null, null, true, null, null)));
        var service = new SimpleFormService(
            new DefinitionRepository(), new VersionRepository(), submissions,
            new WorkflowDefinitionService(new WorkflowDefinitionRepository()),
            completionHandlers: [new SealRequestNotificationHandler(new NotificationService(notifications), directory)]);
        var definition = CreateApprovalDefinition();
        var instanceService = new WorkflowInstanceService(new InstanceRepository());
        var instance = instanceService.Start(definition, nameof(SimpleFormSubmission), submission.Id);
        var tasks = new WorkflowTaskService(new TaskRepository(), instanceService,
            new WorkflowActionExecutor([new SimpleFormSubmissionWorkflowActionHandler(submissions, service)]));
        var approvalNode = definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval);
        var task = tasks.CreateApprovalTask(instance, approvalNode.Id, "印章申请审批", "admin");

        tasks.Approve(task, "admin", "同意");
        service.ApplyApproval(submission);

        Assert.Equal(SimpleFormSubmissionStatus.Approved, submission.Status);
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        var notification = Assert.Single(notifications.Items);
        Assert.Equal("seal.operator", notification.Recipient);
        Assert.Equal("印章申请已批准", notification.Title);

        service.ApplyApproval(submission);
        Assert.Single(notifications.Items);
    }

    private static WorkflowDefinition CreateApprovalDefinition()
    {
        var definition = new WorkflowDefinition(WorkflowBindingCodes.SimpleSealRequestApproval, "印章申请审批");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批",
            configJson: "{\"approver\":\"admin\",\"onApproved\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Approved\"}}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        return definition;
    }

    private sealed class SubmissionRepository(params SimpleFormSubmission[] seed) : ISimpleFormSubmissionRepository
    {
        private readonly List<SimpleFormSubmission> items = [.. seed];
        public IReadOnlyList<SimpleFormSubmission> List(Guid? applicantUserId = null, Guid? definitionId = null) => items;
        public SimpleFormSubmission? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public void Add(SimpleFormSubmission item) => items.Add(item);
        public void Update(SimpleFormSubmission item) { }
    }

    private sealed class DefinitionRepository : ISimpleFormDefinitionRepository
    {
        public IReadOnlyList<SimpleFormDefinition> List() => [];
        public SimpleFormDefinition? Get(Guid id) => null;
        public void Add(SimpleFormDefinition item) { }
        public void Update(SimpleFormDefinition item) { }
    }

    private sealed class VersionRepository : ISimpleFormDefinitionVersionRepository
    {
        public IReadOnlyList<SimpleFormDefinitionVersion> List(Guid? definitionId = null) => [];
        public SimpleFormDefinitionVersion? Get(Guid id) => null;
        public void Add(SimpleFormDefinitionVersion item) { }
        public void Update(SimpleFormDefinitionVersion item) { }
    }

    private sealed class WorkflowDefinitionRepository : IWorkflowDefinitionRepository
    {
        public IReadOnlyList<WorkflowDefinition> List(string? code = null, WorkflowDefinitionStatus? status = null) => [];
        public void Add(WorkflowDefinition definition) { }
        public bool TryAdd(WorkflowDefinition definition) => true;
        public void Update(WorkflowDefinition definition) { }
        public void Remove(Guid id) { }
    }

    private sealed class DirectoryRepository(params EmployeeDirectoryEntry[] entries) : IEmployeeDirectoryRepository
    {
        public IReadOnlyList<EmployeeDirectoryEntry> List() => entries;
        public IReadOnlyList<EmployeeDirectoryOrganization> ListOrganizations() => [];
    }

    private sealed class NotificationRepository : INotificationRepository
    {
        public List<WorkNotification> Items { get; } = [];
        public IReadOnlyList<WorkNotification> List(string recipient, bool unreadOnly = false) => Items.Where(x => x.Recipient.Equals(recipient, StringComparison.OrdinalIgnoreCase)).ToArray();
        public WorkNotification? FindByDedupeKey(string recipient, string dedupeKey) => Items.FirstOrDefault(x => x.Recipient.Equals(recipient, StringComparison.OrdinalIgnoreCase) && x.DedupeKey == dedupeKey);
        public void Add(WorkNotification notification) => Items.Add(notification);
        public bool TryAdd(WorkNotification notification)
        {
            if (FindByDedupeKey(notification.Recipient, notification.DedupeKey) is not null) return false;
            Items.Add(notification);
            return true;
        }
        public void Update(WorkNotification notification) { }
        public int Delete(string recipient, IReadOnlyCollection<Guid> notificationIds) => 0;
    }

    private sealed class AttachmentRepository : IAttachmentRepository
    {
        public IReadOnlyList<BusinessAttachment> List(string? businessType = null, Guid? businessId = null, bool includeDeleted = false) => [];
        public void Add(BusinessAttachment item) { }
        public void Update(BusinessAttachment item) { }
    }

    private sealed class AttachmentAuditRepository : IAttachmentAuditRepository
    {
        public IReadOnlyList<AttachmentAuditEntry> List(Guid? attachmentId = null, Guid? businessId = null) => [];
        public void Add(AttachmentAuditEntry item) { }
    }

    private sealed class CompletionEventRepository : ISimpleFormCompletionEventRepository
    {
        public List<PersistedSimpleFormCompletionEvent> Items { get; } = [];
        public bool TryAdd(PersistedSimpleFormCompletionEvent item)
        {
            if (Items.Any(x => x.SubmissionId == item.SubmissionId && x.EventCode == item.EventCode && x.SubmissionStatus == item.SubmissionStatus)) return false;
            Items.Add(item);
            return true;
        }
        public IReadOnlyList<PersistedSimpleFormCompletionEvent> ListPending(int take) => Items.Where(x => x.Status == SimpleFormCompletionEventStatus.Pending).Take(take).ToArray();
        public void MarkDelivered(Guid id, DateTime deliveredAt) { var item = Items.Single(x => x.Id == id); Items[Items.IndexOf(item)] = item with { Status = SimpleFormCompletionEventStatus.Delivered, DeliveredAt = deliveredAt }; }
        public void MarkFailed(Guid id, string error, DateTime attemptedAt) { var item = Items.Single(x => x.Id == id); Items[Items.IndexOf(item)] = item with { RetryCount = item.RetryCount + 1, LastError = error }; }
    }

    private sealed class RecordingCompletionHandler(bool failFirst) : ISimpleFormCompletionHandler
    {
        public string EventCode => "TEST_EVENT";
        public int CallCount { get; private set; }
        public void Handle(SimpleFormCompletionContext context)
        {
            CallCount++;
            if (failFirst && CallCount == 1) throw new InvalidOperationException("模拟失败");
        }
    }

    private sealed class InstanceRepository : IWorkflowInstanceRepository
    {
        private readonly List<WorkflowInstance> items = [];
        public IReadOnlyList<WorkflowInstance> List(string? businessType = null, Guid? businessId = null, WorkflowInstanceStatus? status = null) => items.Where(x => businessType is null || x.BusinessType == businessType).Where(x => businessId is null || x.BusinessId == businessId).Where(x => status is null || x.Status == status).ToArray();
        public void Add(WorkflowInstance item) => items.Add(item);
        public bool TryAdd(WorkflowInstance item) { if (items.Any(x => x.Id == item.Id)) return false; items.Add(item); return true; }
        public void Update(WorkflowInstance item) { }
        public bool TryUpdate(WorkflowInstance item) { item.MarkPersistedRevision(item.Revision + 1); return true; }
    }

    private sealed class TaskRepository : IWorkflowTaskRepository
    {
        private readonly List<WorkflowTask> items = [];
        public IReadOnlyList<WorkflowTask> List(Guid? instanceId = null, string? assignee = null, WorkflowTaskStatus? status = null) => items.Where(x => instanceId is null || x.InstanceId == instanceId).Where(x => assignee is null || x.Assignee.Equals(assignee, StringComparison.OrdinalIgnoreCase)).Where(x => status is null || x.Status == status).ToArray();
        public void Add(WorkflowTask item) => items.Add(item);
        public bool TryAdd(WorkflowTask item) { if (items.Any(x => x.Id == item.Id)) return false; items.Add(item); return true; }
        public void Update(WorkflowTask item) { }
        public bool TryUpdate(WorkflowTask item) { item.MarkPersistedRevision(item.Revision + 1); return true; }
    }
}
