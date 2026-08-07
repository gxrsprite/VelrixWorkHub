using VelrixWorkHub.Application.WorkItems;
using VelrixWorkHub.Application.Settlements;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class UnifiedTodoServiceTests
{
    [Fact]
    public void Build_IncludesOpenTasksFollowUpsAndExpiringActiveContractsInPriorityOrder()
    {
        var today = new DateOnly(2026, 7, 13);
        var task = new WorkTask("今日任务", dueDate: today);
        var done = new WorkTask("已完成任务", dueDate: today); done.Complete();
        var followUp = new CustomerFollowUp(Guid.NewGuid(), null, FollowUpType.Phone, "逾期回访", today.AddDays(-1));
        var expiring = new SalesContract(Guid.NewGuid(), null, "CT-SOON", "即将到期合同", 100m, today.AddDays(-20), today.AddDays(10)); expiring.Activate();
        var distant = new SalesContract(Guid.NewGuid(), null, "CT-LATER", "远期合同", 100m, today, today.AddDays(31)); distant.Activate();
        var issue = new PmsProjectIssue(Guid.NewGuid(), PmsProjectIssueKind.Risk, "项目风险", "风险说明", "项目经理", PmsProjectIssuePriority.High, today.AddDays(2));
        var closedIssue = new PmsProjectIssue(Guid.NewGuid(), PmsProjectIssueKind.Issue, "已关闭问题", null, null, PmsProjectIssuePriority.Medium, today.AddDays(1)); closedIssue.SetStatus(PmsProjectIssueStatus.Closed);

        var items = UnifiedTodoService.Build(today, [task, done], [followUp], [expiring, distant], [issue, closedIssue]);

        Assert.Collection(items,
            item => { Assert.Equal(UnifiedTodoSource.CustomerFollowUp, item.Source); Assert.True(item.IsOverdue(today)); },
            item => { Assert.Equal(UnifiedTodoSource.ProjectIssue, item.Source); Assert.Equal("Pms/Issue?projectId=" + issue.ProjectId, item.Href); },
            item => Assert.Equal(UnifiedTodoSource.Task, item.Source),
            item => { Assert.Equal(UnifiedTodoSource.Contract, item.Source); Assert.Equal("Crm/ContractLedger/" + expiring.Id, item.Href); });
    }

    [Fact]
    public void Build_IncludesOutstandingReceivableAndPayableWithSettlementLinks()
    {
        var today = new DateOnly(2026, 7, 13);
        var receivable = new SettlementOrderBalance(Guid.NewGuid(), "SO-001", ErpSettlementKind.Receivable, 100m, 40m);
        var payable = new SettlementOrderBalance(Guid.NewGuid(), "PO-001", ErpSettlementKind.Payable, 80m, 0m);
        var settled = new SettlementOrderBalance(Guid.NewGuid(), "SO-SETTLED", ErpSettlementKind.Receivable, 50m, 50m);

        var items = UnifiedTodoService.Build(today, [], [], [], settlementBalances: [receivable, payable, settled]);

        Assert.Collection(items,
            item =>
            {
                Assert.Equal(UnifiedTodoSource.Settlement, item.Source);
                Assert.Contains("待付 ¥80.00", item.Title);
                Assert.Equal("Erp/Settlement?orderId=" + payable.OrderId + "&kind=Payable", item.Href);
            },
            item =>
            {
                Assert.Equal(UnifiedTodoSource.Settlement, item.Source);
                Assert.Contains("待收 ¥60.00", item.Title);
                Assert.Equal("Erp/Settlement?orderId=" + receivable.OrderId + "&kind=Receivable", item.Href);
            });
    }

    [Fact]
    public void Build_UsesSettlementDueDateToRaiseOnlyOverdueBalances()
    {
        var today = new DateOnly(2026, 7, 19);
        var overdue = new SettlementOrderBalance(Guid.CreateVersion7(), "SO-OVERDUE", ErpSettlementKind.Receivable, 100m, 0m) { DueDate = today.AddDays(-1) };
        var future = new SettlementOrderBalance(Guid.CreateVersion7(), "SO-FUTURE", ErpSettlementKind.Receivable, 100m, 0m) { DueDate = today.AddDays(5) };

        var items = UnifiedTodoService.Build(today, [], [], [], settlementBalances: [overdue, future]);

        Assert.Equal(UnifiedTodoPriority.High, items[0].Priority);
        Assert.Equal(today.AddDays(-1), items[0].DueDate);
        Assert.Equal(UnifiedTodoPriority.Normal, items[1].Priority);
        Assert.Equal(today.AddDays(5), items[1].DueDate);
    }

    [Fact]
    public void Build_ProjectsModuleAndPriorityAndSortsCriticalItemsFirst()
    {
        var today = new DateOnly(2026, 7, 13);
        var task = new WorkTask("普通任务", dueDate: today.AddDays(1));
        var issue = new PmsProjectIssue(Guid.NewGuid(), PmsProjectIssueKind.Risk, "紧急风险", "影响上线", "项目经理", PmsProjectIssuePriority.Critical, today.AddDays(3));
        var payable = new SettlementOrderBalance(Guid.NewGuid(), "PO-PRIORITY", ErpSettlementKind.Payable, 100m, 0m);

        var items = UnifiedTodoService.Build(today, [task], [], [], [issue], settlementBalances: [payable]);

        Assert.Equal(UnifiedTodoSource.ProjectIssue, items[0].Source);
        Assert.Equal(UnifiedTodoModule.Pms, items[0].Module);
        Assert.Equal(UnifiedTodoPriority.Critical, items[0].Priority);
        Assert.Equal(UnifiedTodoModule.Erp, items[1].Module);
        Assert.Equal(UnifiedTodoPriority.High, items[1].Priority);
        Assert.Equal(UnifiedTodoModule.Oa, items[2].Module);
        Assert.Equal(UnifiedTodoPriority.Normal, items[2].Priority);
    }

    [Fact]
    public void Build_MapsLmsWorkflowApprovalsToLmsModuleAndDeepLink()
    {
        var businessId = Guid.CreateVersion7();
        var task = WorkflowTask.Rehydrate(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), 1, Guid.CreateVersion7(), "许可证审批",
            nameof(LmsLicenseRequest), businessId, "admin", WorkflowTaskStatus.Pending, null, null, DateTime.Now, null);

        var item = Assert.Single(UnifiedTodoService.Build(new DateOnly(2026, 7, 18), [], [], [], workflowTasks: [task]));

        Assert.Equal(UnifiedTodoModule.Lms, item.Module);
        Assert.Equal(UnifiedTodoPriority.Critical, item.Priority);
        Assert.Equal($"Workflow/Inbox?assignee=admin&businessType={nameof(LmsLicenseRequest)}&businessId={businessId}", item.Href);
        Assert.Contains("LMS 许可证申请审批", item.Detail);
    }

    [Fact]
    public void Build_MapsCashAdvanceRepaymentWorkflowApprovalsToOaModule()
    {
        var businessId = Guid.CreateVersion7();
        var task = WorkflowTask.Rehydrate(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), 1, Guid.CreateVersion7(), "还款审批",
            nameof(OaCashAdvanceRepayment), businessId, "finance", WorkflowTaskStatus.Pending, null, null, DateTime.Now, null);

        var item = Assert.Single(UnifiedTodoService.Build(new DateOnly(2026, 7, 18), [], [], [], workflowTasks: [task]));

        Assert.Equal(UnifiedTodoModule.Oa, item.Module);
        Assert.Equal(UnifiedTodoPriority.Critical, item.Priority);
        Assert.Equal($"Workflow/Inbox?assignee=finance&businessType={nameof(OaCashAdvanceRepayment)}&businessId={businessId}", item.Href);
        Assert.Contains("OA 借款还款审批", item.Detail);
    }

    [Fact]
    public void Build_DeduplicatesRepeatedSourceItemsByStableSourceKey()
    {
        var today = new DateOnly(2026, 7, 18);
        var sourceId = Guid.CreateVersion7();
        var first = new WorkTask("重复事项", dueDate: today.AddDays(2)) { Id = sourceId };
        var duplicate = new WorkTask("重复事项（旧副本）", dueDate: today) { Id = sourceId };

        var items = UnifiedTodoService.Build(today, [first, duplicate], [], []);

        var item = Assert.Single(items);
        Assert.Equal(sourceId, item.SourceId);
        Assert.Equal("重复事项（旧副本）", item.Title);
        Assert.Equal(today, item.DueDate);
    }

    [Fact]
    public void Build_IncludesOverdueOpenProjectPhasesWithProjectDeepLink()
    {
        var today = new DateOnly(2026, 7, 19);
        var projectId = Guid.CreateVersion7();
        var overdue = new PmsProjectPhase(projectId, "方案评审", PmsProjectPhaseKind.Milestone, 1, today.AddDays(-1), today.AddDays(-1));
        overdue.SetStatus(PmsProjectPhaseStatus.Active);
        overdue.SetPercentComplete(60);
        var completed = new PmsProjectPhase(projectId, "已完成节点", PmsProjectPhaseKind.Phase, 2, today.AddDays(-10), today.AddDays(-2));
        completed.SetStatus(PmsProjectPhaseStatus.Active);
        completed.SetPercentComplete(100);
        var future = new PmsProjectPhase(projectId, "未来节点", PmsProjectPhaseKind.Phase, 3, today, today.AddDays(2));

        var item = Assert.Single(UnifiedTodoService.Build(today, [], [], [], phases: [overdue, completed, future]));

        Assert.Equal(UnifiedTodoSource.ProjectPhase, item.Source);
        Assert.Equal(UnifiedTodoPriority.High, item.Priority);
        Assert.Equal(today.AddDays(-1), item.DueDate);
        Assert.Equal($"Pms/Phase?projectId={projectId}", item.Href);
        Assert.Contains("完成度 60%", item.Detail);
    }

    [Fact]
    public void Build_IncludesActiveProductBelowSafetyStockAsErpRisk()
    {
        var today = new DateOnly(2026, 7, 19);
        var productId = Guid.CreateVersion7();
        var item = Assert.Single(UnifiedTodoService.Build(today, [], [], [], inventoryRisks:
        [
            new InventoryRiskTodo(productId, "标准服务包", 10m, 3m),
            new InventoryRiskTodo(Guid.CreateVersion7(), "库存充足", 10m, 10m),
            new InventoryRiskTodo(Guid.CreateVersion7(), "未启用提醒", 0m, 0m)
        ]));

        Assert.Equal(UnifiedTodoSource.InventoryRisk, item.Source);
        Assert.Equal(UnifiedTodoModule.Erp, item.Module);
        Assert.Equal(UnifiedTodoPriority.High, item.Priority);
        Assert.Equal(today, item.DueDate);
        Assert.Equal("Erp/Product", item.Href);
        Assert.Contains("当前 3.00 / 安全线 10.00", item.Detail);
    }

    [Fact]
    public void Build_PreservesModuleAndPriorityDimensionsForCombinedDashboardFilters()
    {
        var today = new DateOnly(2026, 7, 19);
        var criticalIssue = new PmsProjectIssue(Guid.CreateVersion7(), PmsProjectIssueKind.Risk, "紧急发布风险", "阻断上线", "项目经理", PmsProjectIssuePriority.Critical, today.AddDays(1));
        var highBalance = new SettlementOrderBalance(Guid.CreateVersion7(), "PO-HIGH", ErpSettlementKind.Payable, 100m, 0m) { DueDate = today.AddDays(-1) };
        var normalTask = new WorkTask("普通跟进任务", dueDate: today.AddDays(2));

        var items = UnifiedTodoService.Build(today, [normalTask], [], [], [criticalIssue], settlementBalances: [highBalance]);

        Assert.Equal(3, items.Count);
        Assert.Equal(1, items.Count(x => x.Module == UnifiedTodoModule.Pms && x.Priority == UnifiedTodoPriority.Critical));
        Assert.Equal(1, items.Count(x => x.Module == UnifiedTodoModule.Erp && x.Priority == UnifiedTodoPriority.High));
        Assert.Equal(1, items.Count(x => x.Module == UnifiedTodoModule.Oa && x.Priority == UnifiedTodoPriority.Normal));
    }

    [Fact]
    public void Filter_CombinesModuleAndPriorityWithoutChangingSourceOrder()
    {
        var first = new UnifiedTodoItem(UnifiedTodoSource.Task, Guid.CreateVersion7(), "PMS 高", "项目", new DateOnly(2026, 7, 19), "Pms/Project")
        {
            ModuleOverride = UnifiedTodoModule.Pms,
            Priority = UnifiedTodoPriority.High
        };
        var second = new UnifiedTodoItem(UnifiedTodoSource.Task, Guid.CreateVersion7(), "PMS 普通", "项目", new DateOnly(2026, 7, 20), "Pms/Project")
        {
            ModuleOverride = UnifiedTodoModule.Pms,
            Priority = UnifiedTodoPriority.Normal
        };
        var third = new UnifiedTodoItem(UnifiedTodoSource.Task, Guid.CreateVersion7(), "ERP 高", "采购", new DateOnly(2026, 7, 19), "Erp/PurchaseOrder")
        {
            ModuleOverride = UnifiedTodoModule.Erp,
            Priority = UnifiedTodoPriority.High
        };

        var result = UnifiedTodoService.Filter([first, second, third], UnifiedTodoModule.Pms, UnifiedTodoPriority.High);

        var item = Assert.Single(result);
        Assert.Same(first, item);
    }
}
