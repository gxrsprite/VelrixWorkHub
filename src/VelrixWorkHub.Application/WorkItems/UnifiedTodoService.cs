using VelrixWorkHub.Domain;
using VelrixWorkHub.Application.Settlements;

namespace VelrixWorkHub.Application.WorkItems;

public enum UnifiedTodoSource { Task, CustomerFollowUp, Contract, ProjectIssue, ProjectPhase, InventoryRisk, Settlement, WorkflowApproval }
public enum UnifiedTodoModule { Oa, Crm, Erp, Pms, Lms }
public enum UnifiedTodoPriority { Normal, High, Critical }

public sealed record InventoryRiskTodo(Guid ProductId, string ProductName, decimal SafetyStock, decimal Quantity);

public sealed record UnifiedTodoItem(UnifiedTodoSource Source, Guid SourceId, string Title, string Detail, DateOnly DueDate, string Href)
{
    public UnifiedTodoModule? ModuleOverride { get; init; }
    public UnifiedTodoModule Module => Source switch
    {
        _ when ModuleOverride is not null => ModuleOverride.Value,
        UnifiedTodoSource.Task => UnifiedTodoModule.Oa,
        UnifiedTodoSource.CustomerFollowUp or UnifiedTodoSource.Contract => UnifiedTodoModule.Crm,
        UnifiedTodoSource.InventoryRisk or UnifiedTodoSource.Settlement => UnifiedTodoModule.Erp,
        _ => UnifiedTodoModule.Pms
    };

    public UnifiedTodoPriority Priority { get; init; } = UnifiedTodoPriority.Normal;
    public bool IsOverdue(DateOnly today) => DueDate < today;
}

public static class UnifiedTodoService
{
    public static IReadOnlyList<UnifiedTodoItem> Build(DateOnly today, IEnumerable<WorkTask> tasks, IEnumerable<CustomerFollowUp> followUps, IEnumerable<SalesContract> contracts, IEnumerable<PmsProjectIssue>? issues = null, int contractReminderDays = 30, IEnumerable<SettlementOrderBalance>? settlementBalances = null, IEnumerable<WorkflowTask>? workflowTasks = null, IEnumerable<PmsProjectPhase>? phases = null, IEnumerable<InventoryRiskTodo>? inventoryRisks = null)
    {
        if (contractReminderDays < 0) throw new ArgumentOutOfRangeException(nameof(contractReminderDays));
        var reminderEnd = today.AddDays(contractReminderDays);
        var items = new List<UnifiedTodoItem>();
        items.AddRange(tasks.Where(x => x.Status != WorkTaskStatus.Done && x.DueDate is not null).Select(x => new UnifiedTodoItem(UnifiedTodoSource.Task, x.Id, x.Title, "OA 任务", x.DueDate!.Value, "Oa/Task") { Priority = DuePriority(x.DueDate.Value, today) }));
        items.AddRange(followUps.Where(x => x.NextFollowUpDate is not null).Select(x => new UnifiedTodoItem(UnifiedTodoSource.CustomerFollowUp, x.Id, x.Content, "CRM 客户跟进", x.NextFollowUpDate!.Value, "Crm/FollowUp") { Priority = DuePriority(x.NextFollowUpDate.Value, today) }));
        items.AddRange(contracts.Where(x => x.Status == ContractStatus.Active && x.EndDate <= reminderEnd).Select(x => new UnifiedTodoItem(UnifiedTodoSource.Contract, x.Id, $"{x.ContractNo} · {x.Title}", "CRM 合同到期", x.EndDate, $"Crm/ContractLedger/{x.Id}") { Priority = DuePriority(x.EndDate, today) }));
        items.AddRange((issues ?? []).Where(x => x.Status is PmsProjectIssueStatus.Open or PmsProjectIssueStatus.InProgress && x.DueDate is not null).Select(x => new UnifiedTodoItem(UnifiedTodoSource.ProjectIssue, x.Id, x.Title, $"PMS {(x.Kind == PmsProjectIssueKind.Risk ? "风险" : "问题")} · {IssuePriorityLabel(x.Priority)}", x.DueDate!.Value, $"Pms/Issue?projectId={x.ProjectId}") { Priority = IssuePriority(x.Priority, x.DueDate.Value, today) }));
        items.AddRange((phases ?? []).Where(x => x.Status is PmsProjectPhaseStatus.Planned or PmsProjectPhaseStatus.Active && x.PlannedEnd < today).Select(x => new UnifiedTodoItem(UnifiedTodoSource.ProjectPhase, x.Id, $"{x.Name} · 项目节点逾期", $"PMS {(x.Kind == PmsProjectPhaseKind.Milestone ? "里程碑" : "阶段")} · 完成度 {x.PercentComplete}%", x.PlannedEnd, $"Pms/Phase?projectId={x.ProjectId}") { Priority = UnifiedTodoPriority.High }));
        items.AddRange((inventoryRisks ?? []).Where(x => x.SafetyStock > 0 && x.Quantity < x.SafetyStock).Select(x => new UnifiedTodoItem(UnifiedTodoSource.InventoryRisk, x.ProductId, $"{x.ProductName} · 库存低于安全线", $"ERP 库存 · 当前 {x.Quantity:N2} / 安全线 {x.SafetyStock:N2}", today, "Erp/Product") { Priority = UnifiedTodoPriority.High }));
        items.AddRange((settlementBalances ?? []).Where(x => x.RemainingAmount > 0).Select(x => { var dueDate = x.DueDate ?? today; return new UnifiedTodoItem(UnifiedTodoSource.Settlement, x.OrderId, $"{x.OrderNo} · 待{(x.Kind == ErpSettlementKind.Receivable ? "收" : "付")} ¥{x.RemainingAmount:N2}", $"ERP {(x.Kind == ErpSettlementKind.Receivable ? "客户应收" : "供应商应付")}", dueDate, $"Erp/Settlement?orderId={x.OrderId}&kind={x.Kind}") { Priority = x.DueDate is null ? UnifiedTodoPriority.High : DuePriority(dueDate, today) }; }));
        items.AddRange((workflowTasks ?? []).Where(x => x.Status == WorkflowTaskStatus.Pending).Select(x => new UnifiedTodoItem(UnifiedTodoSource.WorkflowApproval, x.Id, $"审批 · {x.NodeName}", $"{WorkflowModuleLabel(x.BusinessType)} · {x.Assignee}", today, $"Workflow/Inbox?assignee={Uri.EscapeDataString(x.Assignee)}&businessType={Uri.EscapeDataString(x.BusinessType)}&businessId={Uri.EscapeDataString(x.BusinessId.ToString())}") { Priority = UnifiedTodoPriority.Critical, ModuleOverride = WorkflowModule(x.BusinessType) }));
        return items
            .GroupBy(x => (x.Source, x.SourceId))
            .Select(group => group
                .OrderByDescending(x => x.Priority)
                .ThenBy(x => x.DueDate)
                .ThenBy(x => x.Title, StringComparer.Ordinal)
                .First())
            .OrderByDescending(x => x.Priority)
            .ThenByDescending(x => x.IsOverdue(today))
            .ThenBy(x => x.DueDate)
            .ThenBy(x => x.Source)
            .ThenBy(x => x.Title)
            .ToArray();
    }

    public static IReadOnlyList<UnifiedTodoItem> Filter(IEnumerable<UnifiedTodoItem> items, UnifiedTodoModule? module = null, UnifiedTodoPriority? priority = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items
            .Where(x => (module is null || x.Module == module.Value) && (priority is null || x.Priority == priority.Value))
            .ToArray();
    }

    private static UnifiedTodoModule WorkflowModule(string businessType) => businessType switch
    {
        nameof(SalesContract) => UnifiedTodoModule.Crm,
        nameof(PmsProjectChange) => UnifiedTodoModule.Pms,
        nameof(LmsLicenseRequest) or nameof(LmsLicenseReplacementRequest) => UnifiedTodoModule.Lms,
        nameof(OaLeaveRequest) or nameof(OaOvertimeRequest) or nameof(OaVehicleUseRequest) or nameof(OaAssetRequest) or nameof(OaProcurementRequest) or nameof(OaPaymentRequest) or nameof(OaCashAdvance) or nameof(OaCashAdvanceRepayment) or nameof(OaExpenseReimbursement) => UnifiedTodoModule.Oa,
        _ => UnifiedTodoModule.Erp
    };

    private static string WorkflowModuleLabel(string businessType) => WorkflowModule(businessType) switch
    {
        UnifiedTodoModule.Crm => "CRM 合同审批",
        UnifiedTodoModule.Pms => "PMS 变更审批",
        UnifiedTodoModule.Lms => businessType == nameof(LmsLicenseReplacementRequest) ? "LMS 授权替代审批" : "LMS 许可证申请审批",
        _ when businessType == nameof(OaProcurementRequest) => "OA 采购申请审批",
        _ when businessType == nameof(OaLeaveRequest) => "OA 请假审批",
        _ when businessType == nameof(OaOvertimeRequest) => "OA 加班审批",
        _ when businessType == nameof(OaVehicleUseRequest) => "OA 用车审批",
        _ when businessType == nameof(OaAssetRequest) => "OA 资产领用审批",
        _ when businessType == nameof(OaPaymentRequest) => "OA 付款申请审批",
        _ when businessType == nameof(OaCashAdvance) => "OA 借款备用金审批",
        _ when businessType == nameof(OaCashAdvanceRepayment) => "OA 借款还款审批",
        _ when businessType == nameof(OaExpenseReimbursement) => "OA 费用报销审批",
        _ when businessType == nameof(PurchaseOrder) => "ERP 采购订单审批",
        _ when businessType == nameof(SalesOrder) => "ERP 销售订单审批",
        _ => "ERP 核销审批"
    };

    private static UnifiedTodoPriority DuePriority(DateOnly dueDate, DateOnly today) => dueDate < today ? UnifiedTodoPriority.High : UnifiedTodoPriority.Normal;

    private static UnifiedTodoPriority IssuePriority(PmsProjectIssuePriority priority, DateOnly dueDate, DateOnly today)
    {
        if (priority == PmsProjectIssuePriority.Critical) return UnifiedTodoPriority.Critical;
        if (priority == PmsProjectIssuePriority.High || dueDate < today) return UnifiedTodoPriority.High;
        return UnifiedTodoPriority.Normal;
    }

    private static string IssuePriorityLabel(PmsProjectIssuePriority priority) => priority switch
    {
        PmsProjectIssuePriority.Low => "低",
        PmsProjectIssuePriority.Medium => "中",
        PmsProjectIssuePriority.High => "高",
        PmsProjectIssuePriority.Critical => "紧急",
        _ => priority.ToString()
    };
}
