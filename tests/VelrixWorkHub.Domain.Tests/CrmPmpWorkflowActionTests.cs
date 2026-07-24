using VelrixWorkHub.Application.Contracts;
using VelrixWorkHub.Application.PmpProjects;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class CrmPmpWorkflowActionTests
{
    [Fact]
    public void ContractApproval_ExecutesStatusAction_AndIsIdempotent()
    {
        var contract = new SalesContract(Guid.CreateVersion7(), null, "CT-WORKFLOW-ACTION", "合同动作", 100m, Today, Today.AddDays(30));
        var repository = new ContractRepository(contract);
        var definition = CreateDefinition(WorkflowBindingCodes.ContractApproval, nameof(SalesContract), nameof(ContractStatus.Active));
        var instanceRepository = new InstanceRepository();
        var instanceService = new WorkflowInstanceService(instanceRepository);
        var instance = instanceService.Start(definition, nameof(SalesContract), contract.Id);
        var taskRepository = new TaskRepository();
        var tasks = new WorkflowTaskService(taskRepository, instanceService, new WorkflowActionExecutor([new SalesContractWorkflowActionHandler(repository)]));
        var task = tasks.CreateApprovalTask(instance, definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval).Id, "合同审批", "admin");

        tasks.Approve(task, "admin", "同意");
        new WorkflowActionExecutor([new SalesContractWorkflowActionHandler(repository)]).Execute(instance, task.NodeId, WorkflowActionTrigger.Approved);

        Assert.Equal(ContractStatus.Active, contract.Status);
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
    }

    [Fact]
    public void ContractApproval_Rejection_LeavesContractDraft()
    {
        var contract = new SalesContract(Guid.CreateVersion7(), null, "CT-WORKFLOW-REJECT", "合同拒绝", 100m, Today, Today.AddDays(30));
        var repository = new ContractRepository(contract);
        var definition = CreateDefinition(WorkflowBindingCodes.ContractApproval, nameof(SalesContract), nameof(ContractStatus.Active));
        var instanceRepository = new InstanceRepository();
        var instanceService = new WorkflowInstanceService(instanceRepository);
        var instance = instanceService.Start(definition, nameof(SalesContract), contract.Id);
        var taskRepository = new TaskRepository();
        var tasks = new WorkflowTaskService(taskRepository, instanceService, new WorkflowActionExecutor([new SalesContractWorkflowActionHandler(repository)]));
        var task = tasks.CreateApprovalTask(instance, definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval).Id, "合同审批", "admin");

        tasks.Reject(task, "admin", "条款不完整");

        Assert.Equal(ContractStatus.Draft, contract.Status);
        Assert.Equal(WorkflowInstanceStatus.Rejected, instance.Status);
    }

    [Fact]
    public void ProjectChangeApproval_ExecutesApprovedStatusAction_AndRejectionLeavesProposed()
    {
        var change = new PmpProjectChange(Guid.CreateVersion7(), "范围变更", "客户补充需求", null, "项目经理", DateTime.Now);
        var repository = new ProjectChangeRepository(change);
        var definition = CreateDefinition(WorkflowBindingCodes.ProjectChangeApproval, nameof(PmpProjectChange), nameof(PmpProjectChangeStatus.Approved));
        var instanceRepository = new InstanceRepository();
        var instanceService = new WorkflowInstanceService(instanceRepository);
        var instance = instanceService.Start(definition, nameof(PmpProjectChange), change.Id);
        var taskRepository = new TaskRepository();
        var tasks = new WorkflowTaskService(taskRepository, instanceService, new WorkflowActionExecutor([new PmpProjectChangeWorkflowActionHandler(repository)]));
        var task = tasks.CreateApprovalTask(instance, definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval).Id, "项目变更审批", "admin");

        tasks.Approve(task, "admin", "同意");

        Assert.Equal(PmpProjectChangeStatus.Approved, change.Status);
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
    }

    [Fact]
    public void ContractApprovalHandler_UsesApplicationApprovalEntryPoint()
    {
        var contract = new SalesContract(Guid.CreateVersion7(), null, "CT-WORKFLOW-ENTRY", "合同动作入口", 100m, Today, Today.AddDays(30));
        var repository = new ContractRepository(contract);
        var service = new SalesContractService(repository);
        var definition = CreateDefinition(WorkflowBindingCodes.ContractApproval, nameof(SalesContract), nameof(ContractStatus.Active));
        var instanceService = new WorkflowInstanceService(new InstanceRepository());
        var instance = instanceService.Start(definition, nameof(SalesContract), contract.Id);
        var taskRepository = new TaskRepository();
        var tasks = new WorkflowTaskService(taskRepository, instanceService, new WorkflowActionExecutor([new SalesContractWorkflowActionHandler(repository, service)]));
        var task = tasks.CreateApprovalTask(instance, definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval).Id, "合同审批", "admin");

        tasks.Approve(task, "admin", "同意");

        Assert.Equal(ContractStatus.Active, contract.Status);
    }

    [Fact]
    public void ProjectChangeApprovalHandler_UsesApplicationApprovalEntryPoint()
    {
        var project = new PmpProject("PMP-WORKFLOW-ENTRY", "审批动作入口项目", null, "项目经理", Today, Today.AddDays(30));
        var change = new PmpProjectChange(project.Id, "范围变更入口", "客户补充需求", null, "项目经理", DateTime.Now);
        var repository = new ProjectChangeRepository(change);
        var service = new PmpProjectChangeService(repository, new ProjectRepository(project));
        var definition = CreateDefinition(WorkflowBindingCodes.ProjectChangeApproval, nameof(PmpProjectChange), nameof(PmpProjectChangeStatus.Approved));
        var instanceService = new WorkflowInstanceService(new InstanceRepository());
        var instance = instanceService.Start(definition, nameof(PmpProjectChange), change.Id);
        var taskRepository = new TaskRepository();
        var tasks = new WorkflowTaskService(taskRepository, instanceService, new WorkflowActionExecutor([new PmpProjectChangeWorkflowActionHandler(repository, service)]));
        var task = tasks.CreateApprovalTask(instance, definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval).Id, "项目变更审批", "admin");

        tasks.Approve(task, "admin", "同意");

        Assert.Equal(PmpProjectChangeStatus.Approved, change.Status);
    }

    [Fact]
    public void WorkItemCompletionApproval_ApprovesOrRejectsThroughApplicationEntryPoint()
    {
        var workItem = CreateInProgressWorkItem("验收通过工作项");
        workItem.SetStatus(PmpProjectWorkItemStatus.PendingApproval, "提交验收", DateTime.Now);
        var rejected = CreateInProgressWorkItem("验收驳回工作项");
        rejected.SetStatus(PmpProjectWorkItemStatus.PendingApproval, "提交验收", DateTime.Now);
        var repository = new WorkItemRepository(workItem, rejected);
        var service = new PmpProjectWorkItemService(repository, new ProjectRepository());
        var definition = CreateWorkItemDefinition();
        var instanceService = new WorkflowInstanceService(new InstanceRepository());
        var tasks = new WorkflowTaskService(new TaskRepository(), instanceService, new WorkflowActionExecutor([new PmpProjectWorkItemWorkflowActionHandler(repository, service)]));
        var approvalNode = definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval).Id;

        var approvedInstance = instanceService.Start(definition, nameof(PmpProjectWorkItem), workItem.Id);
        tasks.Approve(tasks.CreateApprovalTask(approvedInstance, approvalNode, "工作项验收", "admin"), "admin", "验收通过");
        var rejectedInstance = instanceService.Start(definition, nameof(PmpProjectWorkItem), rejected.Id);
        tasks.Reject(tasks.CreateApprovalTask(rejectedInstance, approvalNode, "工作项验收", "admin"), "admin", "请补充交付说明");

        Assert.Equal(PmpProjectWorkItemStatus.Completed, workItem.Status);
        Assert.NotNull(workItem.ActualEndAt);
        Assert.Equal(PmpProjectWorkItemStatus.InProgress, rejected.Status);
        Assert.Equal("请补充交付说明", rejected.CompletionRejectionReason);
    }

    private static WorkflowDefinition CreateDefinition(string code, string businessType, string approvedValue)
    {
        var definition = new WorkflowDefinition(code, $"{businessType}动作测试");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: $"{{\"approver\":\"admin\",\"onApproved\":{{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"{approvedValue}\"}}}}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        return definition;
    }

    private static WorkflowDefinition CreateWorkItemDefinition()
    {
        var definition = new WorkflowDefinition(WorkflowBindingCodes.PmpWorkItemCompletionApproval, "工作项验收动作测试");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "验收", configJson: "{\"approver\":\"admin\",\"onApproved\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Completed\"},\"onRejected\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"InProgress\"}}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        return definition;
    }

    private static PmpProjectWorkItem CreateInProgressWorkItem(string title)
    {
        var item = new PmpProjectWorkItem(Guid.CreateVersion7(), null, null, null, title, null, null, null, PmpProjectWorkItemPriority.Medium, null, null, "{}");
        item.SetStatus(PmpProjectWorkItemStatus.Open, null, DateTime.Now);
        item.SetStatus(PmpProjectWorkItemStatus.InProgress, null, DateTime.Now);
        return item;
    }

    private static readonly DateOnly Today = new(2026, 7, 15);

    private sealed class ContractRepository(params SalesContract[] seed) : ISalesContractRepository
    {
        private readonly List<SalesContract> items = [.. seed];
        public IReadOnlyList<SalesContract> List() => items;
        public void Add(SalesContract contract) => items.Add(contract);
        public void Update(SalesContract contract) { }
        public void Remove(Guid contractId) => items.RemoveAll(x => x.Id == contractId);
    }

    private sealed class ProjectChangeRepository(params PmpProjectChange[] seed) : IPmpProjectChangeRepository
    {
        private readonly List<PmpProjectChange> items = [.. seed];
        public IReadOnlyList<PmpProjectChange> List(Guid? projectId = null) => items.Where(x => projectId is null || x.ProjectId == projectId).ToArray();
        public void Add(PmpProjectChange item) => items.Add(item);
        public void Update(PmpProjectChange item) { }
    }

    private sealed class WorkItemRepository(params PmpProjectWorkItem[] seed) : IPmpProjectWorkItemRepository
    {
        private readonly List<PmpProjectWorkItem> items = [.. seed];
        public IReadOnlyList<PmpProjectWorkItem> List(Guid? projectId = null) => items.Where(x => projectId is null || x.ProjectId == projectId).ToArray();
        public void Add(PmpProjectWorkItem item) => items.Add(item);
        public void Update(PmpProjectWorkItem item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class ProjectRepository(params PmpProject[] seed) : IPmpProjectRepository
    {
        private readonly List<PmpProject> items = [.. seed];
        public IReadOnlyList<PmpProject> List() => items;
        public void Add(PmpProject item) => items.Add(item);
        public void Update(PmpProject item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class InstanceRepository : IWorkflowInstanceRepository
    {
        private readonly List<WorkflowInstance> items = [];
        public IReadOnlyList<WorkflowInstance> List(string? businessType = null, Guid? businessId = null, WorkflowInstanceStatus? status = null)
            => items.Where(x => businessType is null || x.BusinessType == businessType).Where(x => businessId is null || x.BusinessId == businessId).Where(x => status is null || x.Status == status).ToArray();
        public void Add(WorkflowInstance instance) => items.Add(instance);
        public bool TryAdd(WorkflowInstance instance) { if (items.Any(x => x.Id == instance.Id)) return false; Add(instance); return true; }
        public void Update(WorkflowInstance instance) { }
        public bool TryUpdate(WorkflowInstance instance) { var nextRevision = checked(instance.Revision + 1); Update(instance); instance.MarkPersistedRevision(nextRevision); return true; }
    }

    private sealed class TaskRepository : IWorkflowTaskRepository
    {
        private readonly List<WorkflowTask> items = [];
        public IReadOnlyList<WorkflowTask> List(Guid? instanceId = null, string? assignee = null, WorkflowTaskStatus? status = null)
            => items.Where(x => instanceId is null || x.InstanceId == instanceId).Where(x => assignee is null || x.Assignee.Equals(assignee, StringComparison.OrdinalIgnoreCase)).Where(x => status is null || x.Status == status).ToArray();
        public void Add(WorkflowTask task) => items.Add(task);
        public bool TryAdd(WorkflowTask task) { if (items.Any(x => x.Id == task.Id)) return false; Add(task); return true; }
        public void Update(WorkflowTask task) { }
        public bool TryUpdate(WorkflowTask task) { var nextRevision = checked(task.Revision + 1); Update(task); task.MarkPersistedRevision(nextRevision); return true; }
    }
}
