using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Workflow;

public static class WorkflowBindingCodes
{
    public const string ContractApproval = "CONTRACT_APPROVAL";
    public const string ProjectChangeApproval = "PMP_CHANGE_APPROVAL";
    public const string SettlementApproval = "ERP_SETTLEMENT_APPROVAL";
    public const string PurchaseOrderApproval = "ERP_PURCHASE_ORDER_APPROVAL";
    public const string SalesOrderApproval = "ERP_SALES_ORDER_APPROVAL";
    public const string LmsLicenseApproval = "LMS_LICENSE_APPROVAL";
    public const string LmsLicenseReplacementApproval = "LMS_LICENSE_REPLACEMENT_APPROVAL";
    public const string ExpenseReimbursementApproval = "OA_EXPENSE_REIMBURSEMENT_APPROVAL";
    public const string CashAdvanceApproval = "OA_CASH_ADVANCE_APPROVAL";
    public const string CashAdvanceRepaymentApproval = "OA_CASH_ADVANCE_REPAYMENT_APPROVAL";
    public const string PaymentRequestApproval = "OA_PAYMENT_REQUEST_APPROVAL";
    public const string ProcurementRequestApproval = "OA_PROCUREMENT_REQUEST_APPROVAL";
    public const string LeaveApproval = "OA_LEAVE_APPROVAL";
    public const string OvertimeApproval = "OA_OVERTIME_APPROVAL";
    public const string PmpWorkItemCompletionApproval = "PMP_WORK_ITEM_COMPLETION_APPROVAL";
    public const string SimpleSealRequestApproval = "SIMPLE_SEAL_REQUEST_APPROVAL";
    public const string VehicleUseApproval = "OA_VEHICLE_USE_APPROVAL";
    public const string AssetRequestApproval = "OA_ASSET_REQUEST_APPROVAL";
}

/// <summary>
/// 将业务对象绑定到已发布流程，并保证同一业务对象不会重复启动运行中的实例。
/// </summary>
public sealed class WorkflowBindingService(WorkflowDefinitionService definitions, WorkflowInstanceService instances, WorkflowTaskService? tasks = null, WorkflowRuntimeService? runtime = null, IWorkflowTransactionBoundary? transactions = null, IServiceProvider? serviceProvider = null)
{
    private WorkflowRuntimeService? ResolvedRuntime => runtime ?? serviceProvider?.GetService(typeof(WorkflowRuntimeService)) as WorkflowRuntimeService;
    public WorkflowInstance StartOrGet(string definitionCode, string businessType, Guid businessId, DateTime? startedAt = null, string? startedBy = null)
    {
        var definition = definitions.List(definitionCode, WorkflowDefinitionStatus.Published)
            .OrderByDescending(x => x.VersionNumber)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"未找到已发布流程：{definitionCode}。");
        var running = instances.List(businessType, businessId, WorkflowInstanceStatus.Running)
            .Where(x => x.DefinitionCode.Equals(definition.Code, StringComparison.OrdinalIgnoreCase))
            .SingleOrDefault();
        if (running is not null)
        {
            // 运行实例必须按自身版本补待办，不能把新版本节点混入旧实例。
            var runningDefinition = definitions.GetVersion(definition.Code, running.DefinitionVersion);
            if (runningDefinition is not null) PrepareRuntime(running, runningDefinition);
            return running;
        }

        var latest = instances.List(businessType, businessId)
            .Where(x => x.DefinitionCode.Equals(definition.Code, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefault();
        if (latest?.Status is WorkflowInstanceStatus.Rejected or WorkflowInstanceStatus.Cancelled)
            return Resubmit(definitionCode, businessType, businessId, startedAt, startedBy);

        try
        {
            return StartAndPrepare(definition, businessType, businessId, startedAt, startedBy);
        }
        catch (WorkflowRunningInstanceConflictException)
        {
            var winner = instances.List(businessType, businessId, WorkflowInstanceStatus.Running)
                .Where(x => x.DefinitionCode.Equals(definition.Code, StringComparison.OrdinalIgnoreCase))
                .SingleOrDefault();
            if (winner is null) throw;
            var winnerDefinition = definitions.GetVersion(definition.Code, winner.DefinitionVersion);
            if (winnerDefinition is not null) PrepareRuntime(winner, winnerDefinition);
            return winner;
        }
    }

    public WorkflowInstance Resubmit(string definitionCode, string businessType, Guid businessId, DateTime? startedAt = null, string? startedBy = null)
    {
        var definition = definitions.List(definitionCode, WorkflowDefinitionStatus.Published)
            .OrderByDescending(x => x.VersionNumber)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"未找到已发布流程：{definitionCode}。");
        var running = instances.List(businessType, businessId, WorkflowInstanceStatus.Running)
            .Where(x => x.DefinitionCode.Equals(definition.Code, StringComparison.OrdinalIgnoreCase))
            .SingleOrDefault();
        if (running is not null)
        {
            var runningDefinition = definitions.GetVersion(definition.Code, running.DefinitionVersion);
            if (runningDefinition is not null) PrepareRuntime(running, runningDefinition);
            return running;
        }

        var previous = instances.List(businessType, businessId)
            .Where(x => x.DefinitionCode.Equals(definition.Code, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("没有可重新提交的历史审批实例。");
        if (previous.Status is not (WorkflowInstanceStatus.Rejected or WorkflowInstanceStatus.Cancelled))
            throw new InvalidOperationException("只有审批拒绝或撤回的实例可以重新提交。");
        if (string.IsNullOrWhiteSpace(startedBy) || !previous.StartedBy.Equals(startedBy.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("只有原流程发起人可以重新提交审批。");

        try
        {
            return StartAndPrepare(definition, businessType, businessId, startedAt, startedBy, previous.Id);
        }
        catch (WorkflowRunningInstanceConflictException)
        {
            var winner = instances.List(businessType, businessId, WorkflowInstanceStatus.Running)
                .Where(x => x.DefinitionCode.Equals(definition.Code, StringComparison.OrdinalIgnoreCase))
                .SingleOrDefault();
            if (winner is null) throw;
            var winnerDefinition = definitions.GetVersion(definition.Code, winner.DefinitionVersion);
            if (winnerDefinition is not null) PrepareRuntime(winner, winnerDefinition);
            return winner;
        }
    }

    private void PrepareRuntime(WorkflowInstance instance, WorkflowDefinition definition)
    {
        if (transactions is null)
        {
            PrepareRuntimeCore(instance, definition);
            return;
        }

        transactions.Execute(() => PrepareRuntimeCore(instance, definition));
    }

    private void PrepareRuntimeCore(WorkflowInstance instance, WorkflowDefinition definition)
    {
        var resolvedRuntime = ResolvedRuntime;
        if (resolvedRuntime is not null)
        {
            resolvedRuntime.Continue(instance);
            if (tasks is not null && instance.Status == WorkflowInstanceStatus.Running && instance.GetNodeType(instance.CurrentNodeId) == WorkflowNodeType.Approval)
                tasks.EnsureCurrentApprovalTask(instance);
            return;
        }
        if (instance.GetNodeType(instance.CurrentNodeId) == WorkflowNodeType.Start)
        {
            var transition = instance.GetOutgoingTransitions().SingleOrDefault(x => x.ConditionKey is null);
            if (transition is not null)
            {
                var targetType = instance.GetNodeType(transition.TargetNodeId);
                if (targetType is WorkflowNodeType.Approval or WorkflowNodeType.End)
                {
                    instances.Advance(instance, transition.TargetNodeId, transition.ConditionKey);
                    if (targetType == WorkflowNodeType.End)
                    {
                        instances.Complete(instance);
                        return;
                    }
                }
            }
        }

        if (tasks is null) return;
        if (instance.GetNodeType(instance.CurrentNodeId) == WorkflowNodeType.Approval)
            tasks.EnsureCurrentApprovalTask(instance);
        else
            tasks.EnsureApprovalTasks(instance, definition);
    }

    private WorkflowInstance StartAndPrepare(WorkflowDefinition definition, string businessType, Guid businessId, DateTime? startedAt, string? startedBy, Guid? previousInstanceId = null)
    {
        EnsureRuntimeAvailable(definition);
        WorkflowInstance? instance = null;
        var createdInstanceIds = new List<Guid>();
        void StartCore()
        {
            instance = instances.StartWithCompensation(definition, businessType, businessId, startedAt, startedBy, previousInstanceId, createdInstanceIds);
            PrepareRuntime(instance, definition);
        }

        if (transactions is null)
        {
            try { StartCore(); }
            catch
            {
                instances.RemoveCreatedInstances(createdInstanceIds);
                throw;
            }
        }
        else
            transactions.Execute(StartCore, _ => instances.RemoveCreatedInstances(createdInstanceIds));
        return instance!;
    }

    private void EnsureRuntimeAvailable(WorkflowDefinition definition)
    {
        if (ResolvedRuntime is not null) return;
        if (definition.Nodes.Any(node => node.Type is not (WorkflowNodeType.Start or WorkflowNodeType.Approval or WorkflowNodeType.End)))
            throw new InvalidOperationException("流程包含图运行时节点，必须注册 WorkflowRuntimeService 后才能启动。");
    }

    public IReadOnlyList<WorkflowInstance> List(string businessType, Guid businessId) => instances.List(businessType, businessId);

    /// <summary>按业务层要求撤回运行实例；不执行业务字段回写动作。</summary>
    public void Withdraw(Guid instanceId, string actor, string? comment = null, DateTime? completedAt = null)
    {
        if (tasks is null) throw new InvalidOperationException("当前未配置流程待办服务，不能撤回流程。");
        tasks.Withdraw(instanceId, actor, comment, completedAt);
    }
}
