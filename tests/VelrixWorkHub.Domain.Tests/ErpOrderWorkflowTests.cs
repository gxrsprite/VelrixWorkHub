using VelrixWorkHub.Application.Contracts;
using VelrixWorkHub.Application.Customers;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.PurchaseOrders;
using VelrixWorkHub.Application.SalesOrders;
using VelrixWorkHub.Application.Settlements;
using VelrixWorkHub.Application.Suppliers;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class ErpOrderWorkflowTests
{
    [Fact]
    public void PurchaseOrderSubmission_RequiresCompletedApproval()
    {
        var supplier = new Supplier("SUP-WORKFLOW-PO", "采购审批供应商", null, null, null);
        var product = new Product("SKU-WORKFLOW-PO", "采购审批商品", "件", 10m, null);
        var order = new PurchaseOrder("PO-WORKFLOW-01", supplier.Id, product.Id, Today, 2m, 10m);
        var repository = new InMemoryPurchaseOrderRepository(order);
        var harness = new WorkflowHarness(WorkflowBindingCodes.PurchaseOrderApproval, new PurchaseOrderWorkflowActionHandler(repository));
        var service = new PurchaseOrderService(
            repository,
            new InMemorySupplierRepository(supplier),
            new InMemoryProductRepository(product),
            null!,
            null!,
            new InMemorySettlementRepository(),
            harness.Approval);

        var error = Assert.Throws<InvalidOperationException>(() => service.SetStatus(order, PurchaseOrderStatus.Submitted));
        Assert.Contains("采购订单提交前必须完成审批", error.Message);

        harness.Binding.StartOrGet(WorkflowBindingCodes.PurchaseOrderApproval, nameof(PurchaseOrder), order.Id);
        var task = Assert.Single(harness.TaskRepository.Items);
        harness.Tasks.Approve(task, "admin", "同意");
        Assert.Equal(PurchaseOrderStatus.Submitted, order.Status);
        service.SetStatus(order, PurchaseOrderStatus.Submitted);

        Assert.Equal(PurchaseOrderStatus.Submitted, order.Status);
        Assert.Equal(WorkflowInstanceStatus.Completed, Assert.Single(harness.Instances.Items).Status);
    }

    [Fact]
    public void SalesOrderSubmission_RequiresCompletedApproval()
    {
        var customer = new Customer("销售审批客户");
        var product = new Product("SKU-WORKFLOW-SO", "销售审批商品", "件", 20m, null);
        var order = new SalesOrder("SO-WORKFLOW-01", customer.Id, product.Id, Today, 3m, 20m);
        var repository = new InMemorySalesOrderRepository(order);
        var harness = new WorkflowHarness(WorkflowBindingCodes.SalesOrderApproval, new SalesOrderWorkflowActionHandler(repository));
        var service = new SalesOrderService(
            repository,
            new InMemoryCustomerRepository(customer),
            new InMemoryProductRepository(product),
            null!,
            null!,
            new SalesContractService(new InMemoryContractRepository()),
            new InMemorySettlementRepository(),
            approval: harness.Approval);

        Assert.Throws<InvalidOperationException>(() => service.SetStatus(order, SalesOrderStatus.Submitted));
        harness.Binding.StartOrGet(WorkflowBindingCodes.SalesOrderApproval, nameof(SalesOrder), order.Id);
        harness.Tasks.Approve(Assert.Single(harness.TaskRepository.Items), "admin", "同意");
        Assert.Equal(SalesOrderStatus.Submitted, order.Status);
        service.SetStatus(order, SalesOrderStatus.Submitted);

        Assert.Equal(SalesOrderStatus.Submitted, order.Status);
        Assert.Equal(WorkflowInstanceStatus.Completed, Assert.Single(harness.Instances.Items).Status);
    }

    [Fact]
    public void OrderCancellation_IsBlockedWhileApprovalRuns_AndAllowedAfterRejection()
    {
        var customer = new Customer("取消门禁客户");
        var product = new Product("SKU-WORKFLOW-CANCEL", "取消门禁商品", "件", 20m, null);
        var order = new SalesOrder("SO-WORKFLOW-CANCEL", customer.Id, product.Id, Today, 1m, 20m);
        var repository = new InMemorySalesOrderRepository(order);
        var harness = new WorkflowHarness(WorkflowBindingCodes.SalesOrderApproval, new SalesOrderWorkflowActionHandler(repository));
        var service = new SalesOrderService(
            repository,
            new InMemoryCustomerRepository(customer),
            new InMemoryProductRepository(product),
            null!,
            null!,
            new SalesContractService(new InMemoryContractRepository()),
            new InMemorySettlementRepository(),
            approval: harness.Approval);

        harness.Binding.StartOrGet(WorkflowBindingCodes.SalesOrderApproval, nameof(SalesOrder), order.Id);
        var runningError = Assert.Throws<InvalidOperationException>(() => service.SetStatus(order, SalesOrderStatus.Cancelled));
        Assert.Contains("审批进行中，暂不能取消销售订单", runningError.Message);

        harness.Tasks.Reject(Assert.Single(harness.TaskRepository.Items), "admin", "库存策略不通过");
        service.SetStatus(order, SalesOrderStatus.Cancelled);

        Assert.Equal(SalesOrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void WorkflowActionHandler_UsesSalesOrderApplicationApprovalEntryPoint()
    {
        var customer = new Customer("审批动作入口客户");
        var product = new Product("SKU-WORKFLOW-ENTRY", "审批动作入口商品", "件", 20m, null);
        var order = new SalesOrder("SO-WORKFLOW-ENTRY", customer.Id, product.Id, Today, 1m, 20m);
        var repository = new InMemorySalesOrderRepository(order);
        var service = new SalesOrderService(
            repository,
            new InMemoryCustomerRepository(customer),
            new InMemoryProductRepository(product),
            null!,
            null!,
            new SalesContractService(new InMemoryContractRepository()),
            new InMemorySettlementRepository());
        var harness = new WorkflowHarness(WorkflowBindingCodes.SalesOrderApproval, new SalesOrderWorkflowActionHandler(repository, service));
        harness.Binding.StartOrGet(WorkflowBindingCodes.SalesOrderApproval, nameof(SalesOrder), order.Id);
        harness.Tasks.Approve(Assert.Single(harness.TaskRepository.Items), "admin", "同意");

        Assert.Equal(SalesOrderStatus.Submitted, order.Status);
    }

    [Fact]
    public void WorkflowActionHandler_UsesPurchaseOrderApplicationApprovalEntryPoint()
    {
        var supplier = new Supplier("SUP-WORKFLOW-ENTRY", "审批动作入口供应商", null, null, null);
        var product = new Product("SKU-WORKFLOW-PO-ENTRY", "审批动作入口采购商品", "件", 20m, null);
        var order = new PurchaseOrder("PO-WORKFLOW-ENTRY", supplier.Id, product.Id, Today, 1m, 20m);
        var repository = new InMemoryPurchaseOrderRepository(order);
        var service = new PurchaseOrderService(
            repository,
            new InMemorySupplierRepository(supplier),
            new InMemoryProductRepository(product),
            null!,
            null!,
            new InMemorySettlementRepository());
        var harness = new WorkflowHarness(WorkflowBindingCodes.PurchaseOrderApproval, new PurchaseOrderWorkflowActionHandler(repository, service));
        harness.Binding.StartOrGet(WorkflowBindingCodes.PurchaseOrderApproval, nameof(PurchaseOrder), order.Id);
        harness.Tasks.Approve(Assert.Single(harness.TaskRepository.Items), "admin", "同意");

        Assert.Equal(PurchaseOrderStatus.Submitted, order.Status);
    }

    private static readonly DateOnly Today = new(2026, 7, 15);

    private sealed class WorkflowHarness
    {
        public WorkflowDefinitionService Definitions { get; }
        public WorkflowInstanceService InstanceService { get; }
        public WorkflowTaskService Tasks { get; }
        public InMemoryTaskRepository TaskRepository { get; }
        public WorkflowBindingService Binding { get; }
        public WorkflowApprovalService Approval { get; }
        public InMemoryInstanceRepository Instances { get; }

        public WorkflowHarness(string code, params IWorkflowActionHandler[] handlers)
        {
            var definitions = new InMemoryDefinitionRepository();
            Definitions = new WorkflowDefinitionService(definitions);
            var definition = Definitions.CreateDraft(code, "ERP 订单审批");
            var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
            var approvalConfig = code is WorkflowBindingCodes.PurchaseOrderApproval or WorkflowBindingCodes.SalesOrderApproval
                ? "{\"approver\":\"admin\",\"onApproved\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Submitted\"}}"
                : "{\"approver\":\"admin\"}";
            var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "订单审批", configJson: approvalConfig);
            var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
            definition.Connect(start.Id, approval.Id);
            definition.Connect(approval.Id, end.Id);
            Definitions.Publish(definition);

            Instances = new InMemoryInstanceRepository();
            InstanceService = new WorkflowInstanceService(Instances);
            TaskRepository = new InMemoryTaskRepository();
            Tasks = new WorkflowTaskService(TaskRepository, InstanceService, new WorkflowActionExecutor(handlers));
            Binding = new WorkflowBindingService(Definitions, InstanceService, Tasks);
            Approval = new WorkflowApprovalService(Binding);
        }
    }

    private sealed class InMemoryDefinitionRepository : IWorkflowDefinitionRepository
    {
        private readonly List<WorkflowDefinition> items = [];
        public IReadOnlyList<WorkflowDefinition> List(string? code = null, WorkflowDefinitionStatus? status = null) => items.Where(x => (code is null || x.Code.Equals(code, StringComparison.OrdinalIgnoreCase)) && (status is null || x.Status == status)).ToArray();
        public void Add(WorkflowDefinition definition) => items.Add(definition);
        public bool TryAdd(WorkflowDefinition definition)
        {
            if (items.Any(x => x.Id == definition.Id || (x.Code.Equals(definition.Code, StringComparison.OrdinalIgnoreCase) && x.VersionNumber == definition.VersionNumber))) return false;
            Add(definition);
            return true;
        }
        public void Update(WorkflowDefinition definition) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class InMemoryInstanceRepository : IWorkflowInstanceRepository
    {
        public List<WorkflowInstance> Items { get; } = [];
        public IReadOnlyList<WorkflowInstance> List(string? businessType = null, Guid? businessId = null, WorkflowInstanceStatus? status = null) => Items.Where(x => (businessType is null || x.BusinessType == businessType) && (businessId is null || x.BusinessId == businessId) && (status is null || x.Status == status)).ToArray();
        public void Add(WorkflowInstance instance) => Items.Add(instance);
        public bool TryAdd(WorkflowInstance instance) { if (Items.Any(x => x.Id == instance.Id)) return false; Add(instance); return true; }
        public void Update(WorkflowInstance instance) { }
        public bool TryUpdate(WorkflowInstance instance) { var nextRevision = checked(instance.Revision + 1); Update(instance); instance.MarkPersistedRevision(nextRevision); return true; }
    }

    private sealed class InMemoryTaskRepository : IWorkflowTaskRepository
    {
        public List<WorkflowTask> Items { get; } = [];
        public IReadOnlyList<WorkflowTask> List(Guid? instanceId = null, string? assignee = null, WorkflowTaskStatus? status = null) => Items.Where(x => (instanceId is null || x.InstanceId == instanceId) && (assignee is null || x.Assignee.Equals(assignee, StringComparison.OrdinalIgnoreCase)) && (status is null || x.Status == status)).ToArray();
        public void Add(WorkflowTask task) => Items.Add(task);
        public bool TryAdd(WorkflowTask task) { if (Items.Any(x => x.Id == task.Id)) return false; Add(task); return true; }
        public void Update(WorkflowTask task) { }
        public bool TryUpdate(WorkflowTask task) { var nextRevision = checked(task.Revision + 1); Update(task); task.MarkPersistedRevision(nextRevision); return true; }
    }

    private sealed class InMemoryPurchaseOrderRepository(params PurchaseOrder[] seed) : IPurchaseOrderRepository
    {
        private readonly List<PurchaseOrder> items = [.. seed];
        public IReadOnlyList<PurchaseOrder> List() => items;
        public void Add(PurchaseOrder item) => items.Add(item);
        public void Update(PurchaseOrder item) { }
    }

    private sealed class InMemorySalesOrderRepository(params SalesOrder[] seed) : ISalesOrderRepository
    {
        private readonly List<SalesOrder> items = [.. seed];
        public IReadOnlyList<SalesOrder> List() => items;
        public void Add(SalesOrder item) => items.Add(item);
        public void Update(SalesOrder item) { }
    }

    private sealed class InMemorySupplierRepository(params Supplier[] seed) : ISupplierRepository
    {
        private readonly List<Supplier> items = [.. seed];
        public IReadOnlyList<Supplier> List() => items;
        public void Add(Supplier item) => items.Add(item);
        public void Update(Supplier item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class InMemoryCustomerRepository(params Customer[] seed) : ICustomerRepository
    {
        private readonly List<Customer> items = [.. seed];
        public IReadOnlyList<Customer> List() => items;
        public void Add(Customer item) => items.Add(item);
        public void Update(Customer item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class InMemoryProductRepository(params Product[] seed) : IProductRepository
    {
        private readonly List<Product> items = [.. seed];
        public IReadOnlyList<Product> List() => items;
        public void Add(Product item) => items.Add(item);
        public void Update(Product item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class InMemorySettlementRepository : ISettlementRepository
    {
        public IReadOnlyList<ErpSettlement> List() => [];
        public void Add(ErpSettlement item) { }
        public void Update(ErpSettlement item) { }
    }

    private sealed class InMemoryContractRepository : ISalesContractRepository
    {
        public IReadOnlyList<SalesContract> List() => [];
        public void Add(SalesContract item) { }
        public void Update(SalesContract item) { }
        public void Remove(Guid id) { }
    }
}
