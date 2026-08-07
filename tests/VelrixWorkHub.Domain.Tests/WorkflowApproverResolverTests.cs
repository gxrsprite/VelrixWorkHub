using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class WorkflowApproverResolverTests
{
    [Fact]
    public void Resolve_ExpandsInitiatorAndKeepsExistingLiteralAssignees()
    {
        var instance = WorkflowInstance.Start(CreateDefinition(), "resolver.document", Guid.CreateVersion7(), startedBy: "Starter");
        var resolver = new DefaultWorkflowApproverResolver();

        var result = resolver.Resolve(instance, "{\"approver\":\"$initiator\",\"approvers\":[\"finance\",\"FINANCE\",\"  \"]}");

        Assert.Equal(["Starter", "finance"], result);
    }

    [Fact]
    public void Resolve_CombinesRoleMembersWithExistingApprovers()
    {
        var instance = WorkflowInstance.Start(CreateDefinition(), "resolver.document", Guid.CreateVersion7(), startedBy: "starter");
        var resolver = new DefaultWorkflowApproverResolver(new RoleLookup());

        var result = resolver.Resolve(instance, "{\"approver\":\"admin\",\"approverRoles\":[\"Finance\",\"finance\"]}");

        Assert.Equal(["admin", "finance-a", "finance-b"], result);
    }

    [Fact]
    public void Resolve_CombinesOrganizationMembersWithExistingApprovers()
    {
        var instance = WorkflowInstance.Start(CreateDefinition(), "resolver.document", Guid.CreateVersion7(), startedBy: "starter");
        var resolver = new DefaultWorkflowApproverResolver(organizationLookup: new OrganizationLookup());

        var result = resolver.Resolve(instance, "{\"approver\":\"admin\",\"approverOrgs\":[\"研发部\",\" 研发部 \"]}");

        Assert.Equal(["admin", "dev-a", "dev-b"], result);
    }

    [Fact]
    public void Resolve_CombinesBusinessFieldMembersWithExistingApprovers()
    {
        var instance = WorkflowInstance.Start(CreateDefinition(), nameof(PmsProjectChange), Guid.CreateVersion7(), startedBy: "starter");
        var resolver = new DefaultWorkflowApproverResolver(businessLookup: new BusinessLookup());

        var result = resolver.Resolve(instance, "{\"approver\":\"admin\",\"approverBusinessFields\":[\"RequesterName\",\"requestername\"]}");

        Assert.Equal(["admin", "requester-a", "requester-b"], result);
    }

    [Fact]
    public void PmsProjectChangeSource_ResolvesRequesterName()
    {
        var change = new PmsProjectChange(Guid.CreateVersion7(), "测试变更", "测试原因", null, " requester ", DateTime.Now);
        var source = new PmsProjectChangeWorkflowApproverSource(new ChangeRepository([change]));
        var instance = WorkflowInstance.Start(CreateDefinition(), nameof(PmsProjectChange), change.Id, startedBy: "starter");

        var result = source.FindUsernames(instance, [nameof(PmsProjectChange.RequesterName)]);

        Assert.Equal(["requester"], result);
    }

    [Fact]
    public void PmsProjectChangeSource_ReturnsEmptyWhenRequesterNameIsMissing()
    {
        var change = new PmsProjectChange(Guid.CreateVersion7(), "测试变更", "测试原因", null, null, DateTime.Now);
        var source = new PmsProjectChangeWorkflowApproverSource(new ChangeRepository([change]));
        var instance = WorkflowInstance.Start(CreateDefinition(), nameof(PmsProjectChange), change.Id, startedBy: "starter");

        var result = source.FindUsernames(instance, [nameof(PmsProjectChange.RequesterName)]);

        Assert.Empty(result);
    }

    [Fact]
    public void EnsureCurrentApprovalTask_RejectsEmptyDynamicApproverResult()
    {
        var definition = CreateDefinition();
        var instances = new WorkflowInstanceService(new InstanceRepository());
        var instance = instances.Start(definition, "resolver.document", Guid.CreateVersion7(), startedBy: "starter");
        instances.Advance(instance, definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval).Id);
        var tasks = new WorkflowTaskService(new TaskRepository(), instances, approverResolver: new EmptyResolver());

        var error = Assert.Throws<InvalidOperationException>(() => tasks.EnsureCurrentApprovalTask(instance));

        Assert.Contains("未解析到可用审批人", error.Message);
    }

    [Fact]
    public void EnsureCurrentApprovalTask_KeepsExistingPendingTasksWhenDynamicMembersChange()
    {
        var definition = CreateDefinition();
        var instances = new WorkflowInstanceService(new InstanceRepository());
        var instance = instances.Start(definition, "resolver.document", Guid.CreateVersion7(), startedBy: "starter");
        var approval = definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval);
        instances.Advance(instance, approval.Id);
        var existing = new WorkflowTask(instance, approval.Id, approval.Name, "former-member");
        var repository = new TaskRepository([existing]);
        var tasks = new WorkflowTaskService(repository, instances, approverResolver: new NewMemberResolver());

        var result = tasks.EnsureCurrentApprovalTask(instance);

        Assert.Empty(result);
        Assert.Single(repository.Items);
        Assert.Equal("former-member", repository.Items[0].Assignee);
    }

    private static WorkflowDefinition CreateDefinition()
    {
        var definition = new WorkflowDefinition("APPROVER_RESOLVER", "审批人解析器");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"admin\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        return definition;
    }

    private sealed class RoleLookup : IWorkflowRoleApproverLookup
    {
        public IReadOnlyList<string> FindUsernames(IReadOnlyCollection<string> roleNames)
        {
            Assert.Single(roleNames);
            Assert.Contains("Finance", roleNames, StringComparer.OrdinalIgnoreCase);
            return ["finance-a", "finance-b", "FINANCE-A"];
        }
    }

    private sealed class OrganizationLookup : IWorkflowOrganizationApproverLookup
    {
        public IReadOnlyList<string> FindUsernames(IReadOnlyCollection<string> organizationNames)
        {
            Assert.Single(organizationNames);
            Assert.Contains("研发部", organizationNames, StringComparer.OrdinalIgnoreCase);
            return ["dev-a", "dev-b", "DEV-A"];
        }
    }

    private sealed class BusinessLookup : IWorkflowBusinessApproverLookup
    {
        public IReadOnlyList<string> FindUsernames(WorkflowInstance instance, IReadOnlyCollection<string> fieldNames)
        {
            Assert.Equal(nameof(PmsProjectChange), instance.BusinessType);
            Assert.Single(fieldNames);
            Assert.Contains(nameof(PmsProjectChange.RequesterName), fieldNames, StringComparer.OrdinalIgnoreCase);
            return ["requester-a", "requester-b", "REQUESTER-A"];
        }
    }

    private sealed class ChangeRepository(IEnumerable<PmsProjectChange> items) : IPmsProjectChangeRepository
    {
        private readonly List<PmsProjectChange> _items = items.ToList();
        public IReadOnlyList<PmsProjectChange> List(Guid? projectId = null) => _items.Where(x => projectId is null || x.ProjectId == projectId).ToArray();
        public void Add(PmsProjectChange item) => _items.Add(item);
        public void Update(PmsProjectChange item) { }
    }

    private sealed class EmptyResolver : IWorkflowApproverResolver
    {
        public IReadOnlyList<string> Resolve(WorkflowInstance instance, string nodeConfigJson) => [];
    }

    private sealed class NewMemberResolver : IWorkflowApproverResolver
    {
        public IReadOnlyList<string> Resolve(WorkflowInstance instance, string nodeConfigJson) => ["new-member"];
    }

    private sealed class InstanceRepository : IWorkflowInstanceRepository
    {
        public IReadOnlyList<WorkflowInstance> List(string? businessType = null, Guid? businessId = null, WorkflowInstanceStatus? status = null) => [];
        public void Add(WorkflowInstance instance) { }
        public bool TryAdd(WorkflowInstance instance) { Add(instance); return true; }
        public void Update(WorkflowInstance instance) { }
        public bool TryUpdate(WorkflowInstance instance) { var nextRevision = checked(instance.Revision + 1); Update(instance); instance.MarkPersistedRevision(nextRevision); return true; }
    }

    private sealed class TaskRepository : IWorkflowTaskRepository
    {
        public TaskRepository(IEnumerable<WorkflowTask>? items = null) => Items = items?.ToList() ?? [];
        public List<WorkflowTask> Items { get; }
        public IReadOnlyList<WorkflowTask> List(Guid? instanceId = null, string? assignee = null, WorkflowTaskStatus? status = null)
            => Items.Where(x => (instanceId is null || x.InstanceId == instanceId)
                && (assignee is null || x.Assignee.Equals(assignee, StringComparison.OrdinalIgnoreCase))
                && (status is null || x.Status == status)).ToArray();
        public void Add(WorkflowTask task) => Items.Add(task);
        public bool TryAdd(WorkflowTask task) { if (Items.Any(x => x.Id == task.Id)) return false; Add(task); return true; }
        public void Update(WorkflowTask task) { }
        public bool TryUpdate(WorkflowTask task) { var nextRevision = checked(task.Revision + 1); Update(task); task.MarkPersistedRevision(nextRevision); return true; }
    }
}
