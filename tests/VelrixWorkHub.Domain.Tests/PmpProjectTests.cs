using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PmpProjectTests
{
    [Fact]
    public void Project_RequiresCodeNameAndValidPlanDates()
    {
        var project = new PmpProject(" PRJ-1 ", " 交付项目 ", null, " 项目经理 ", new DateOnly(2026, 7, 12), new DateOnly(2026, 8, 12));
        Assert.Equal("PRJ-1", project.Code);
        Assert.Equal("交付项目", project.Name);
        Assert.Throws<ArgumentException>(() => new PmpProject("PRJ-2", "项目", null, null, new DateOnly(2026, 8, 12), new DateOnly(2026, 7, 12)));
    }

    [Fact]
    public void Project_PercentCompleteIsBoundedAndCompletesActiveProject()
    {
        var project = new PmpProject("PRJ-1", "交付项目", null, null, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        project.SetStatus(PmpProjectStatus.Active);
        project.SetPercentComplete(100);
        Assert.Equal(PmpProjectStatus.Completed, project.Status);
        Assert.Throws<ArgumentOutOfRangeException>(() => project.SetPercentComplete(101));
    }

    [Fact]
    public void Phase_RequiresProjectNameSequenceAndValidDates()
    {
        var projectId = Guid.CreateVersion7();
        var phase = new PmpProjectPhase(projectId, " 方案确认 ", PmpProjectPhaseKind.Phase, 1, new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 26));
        Assert.Equal("方案确认", phase.Name);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PmpProjectPhase(projectId, "阶段", PmpProjectPhaseKind.Phase, 0, new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 12)));
        Assert.Throws<ArgumentException>(() => new PmpProjectPhase(projectId, "阶段", PmpProjectPhaseKind.Phase, 1, new DateOnly(2026, 7, 26), new DateOnly(2026, 7, 12)));
    }

    [Fact]
    public void Milestone_AtFullPercentBecomesCompleted()
    {
        var milestone = new PmpProjectPhase(Guid.CreateVersion7(), "方案评审", PmpProjectPhaseKind.Milestone, 2, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today));
        milestone.SetPercentComplete(100);
        Assert.Equal(PmpProjectPhaseStatus.Completed, milestone.Status);
    }

    [Fact]
    public void WbsTask_RequiresValidDatesAndMilestoneUsesSingleDay()
    {
        var projectId = Guid.CreateVersion7();
        var task = new PmpWbsTask(projectId, null, " 需求确认 ", " 负责人 ", 1, new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 19), false);
        Assert.Equal("需求确认", task.Title);
        Assert.Throws<ArgumentException>(() => new PmpWbsTask(projectId, null, "里程碑", null, 1, new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 13), true));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PmpWbsTask(projectId, null, "任务", null, 0, new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 12), false));
    }

    [Fact]
    public void WbsTask_AtFullPercentBecomesDone()
    {
        var task = new PmpWbsTask(Guid.CreateVersion7(), null, "需求确认", null, 1, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today), true);
        task.SetPercentComplete(100);
        Assert.Equal(PmpWbsTaskStatus.Done, task.Status);
    }

    [Fact]
    public void ProjectMember_RequiresProjectMemberAndRole()
    {
        var member = new PmpProjectMember(Guid.CreateVersion7(), " 项目经理 ", " 项目主责人 ", true);
        Assert.Equal("项目经理", member.MemberName);
        Assert.Equal("项目主责人", member.RoleName);
        Assert.Throws<ArgumentException>(() => new PmpProjectMember(Guid.Empty, "成员", "角色"));
        Assert.Throws<ArgumentException>(() => new PmpProjectMember(member.ProjectId, "", "角色"));
    }

    [Fact]
    public void ProjectIssue_RequiresTitleAndNormalizesOptionalFields()
    {
        var issue = new PmpProjectIssue(Guid.CreateVersion7(), PmpProjectIssueKind.Risk, " 延期风险 ", " 描述 ", " 负责人 ", PmpProjectIssuePriority.High, new DateOnly(2026, 7, 19));
        Assert.Equal("延期风险", issue.Title);
        Assert.Equal("描述", issue.Description);
        Assert.Equal("负责人", issue.OwnerName);
        Assert.Throws<ArgumentException>(() => new PmpProjectIssue(issue.ProjectId, PmpProjectIssueKind.Issue, "", null, null, PmpProjectIssuePriority.Low, null));
    }

    [Fact]
    public void Baseline_RequiresValidVersionDatesAndPercent()
    {
        var projectId = Guid.CreateVersion7();
        var baseline = new PmpProjectBaseline(projectId, 1, " 立项基线 ", DateTime.Now, new DateOnly(2026, 7, 12), new DateOnly(2026, 8, 12), 25, 2, 5);
        Assert.Equal("立项基线", baseline.Label);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PmpProjectBaseline(projectId, 0, "基线", DateTime.Now, new DateOnly(2026, 7, 12), new DateOnly(2026, 8, 12), 0, 0, 0));
        Assert.Throws<ArgumentException>(() => new PmpProjectBaseline(projectId, 1, "基线", DateTime.Now, new DateOnly(2026, 8, 12), new DateOnly(2026, 7, 12), 0, 0, 0));
    }

    [Fact]
    public void BaselineComparison_ReportsCurrentDeltas()
    {
        var baseline = new PmpProjectBaseline(Guid.CreateVersion7(), 1, "基线", DateTime.Now, new DateOnly(2026, 7, 12), new DateOnly(2026, 8, 12), 25, 2, 5);
        var project = new PmpProject("PRJ-1", "项目", null, null, new DateOnly(2026, 7, 12), new DateOnly(2026, 8, 19));
        project.SetPercentComplete(40);
        var comparison = new VelrixWorkHub.Application.PmpProjects.PmpBaselineComparison(baseline, project, 3, 7);
        Assert.Equal(15, comparison.PercentDelta);
        Assert.Equal(1, comparison.PhaseDelta);
        Assert.Equal(2, comparison.TaskDelta);
        Assert.Equal(7, comparison.PlannedDaysDelta);
    }

    [Fact]
    public void ProjectChange_RequiresTitleAndReasonAndStartsAsProposed()
    {
        var change = new PmpProjectChange(Guid.CreateVersion7(), " 补充范围 ", " 客户新增需求 ", " 增加工期 ", " 申请人 ", DateTime.Now);
        Assert.Equal("补充范围", change.Title);
        Assert.Equal(PmpProjectChangeStatus.Proposed, change.Status);
        Assert.Throws<ArgumentException>(() => new PmpProjectChange(change.ProjectId, "", "原因", null, null, DateTime.Now));
        Assert.Throws<ArgumentException>(() => new PmpProjectChange(change.ProjectId, "标题", "", null, null, DateTime.Now));
    }

    [Fact]
    public void WorkLog_RequiresMemberAndBoundsHours()
    {
        var log = new PmpWorkLog(Guid.CreateVersion7(), null, DateOnly.FromDateTime(DateTime.Today), " 项目经理 ", 6.567m, " 需求确认 ");
        Assert.Equal("项目经理", log.MemberName);
        Assert.Equal(6.57m, log.Hours);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PmpWorkLog(log.ProjectId, null, log.WorkDate, "成员", 24.1m, null));
        Assert.Throws<ArgumentException>(() => new PmpWorkLog(log.ProjectId, null, log.WorkDate, "", 1m, null));
    }

    [Fact]
    public void EvmSnapshot_CalculatesPerformanceIndexes()
    {
        var project = new PmpProject("PRJ-1", "项目", null, null, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        var snapshot = new VelrixWorkHub.Application.PmpProjects.PmpEvmSnapshot(project, null, 16m, 8m, 10m);
        Assert.Equal(0.5m, snapshot.SchedulePerformanceIndex);
        Assert.Equal(0.8m, snapshot.CostPerformanceIndex);
        Assert.Equal(6m, snapshot.VarianceAtCompletion);
    }

    [Fact]
    public void PurchaseOrder_ValidatesReferencesAndCalculatesAmount()
    {
        var order = new PurchaseOrder(" PO-1 ", Guid.CreateVersion7(), Guid.CreateVersion7(), DateOnly.FromDateTime(DateTime.Today), 2.5m, 12.34m);
        Assert.Equal("PO-1", order.OrderNo);
        Assert.Equal(30.85m, order.Amount);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PurchaseOrder("PO-2", order.SupplierId, order.ProductId, order.OrderDate, 0, 1));
        Assert.Throws<ArgumentException>(() => new PurchaseOrder("", order.SupplierId, order.ProductId, order.OrderDate, 1, 1));
    }

    [Fact]
    public void PurchaseOrder_EnforcesStatusFlow()
    {
        var order = new PurchaseOrder("PO-STATUS", Guid.CreateVersion7(), Guid.CreateVersion7(), DateOnly.FromDateTime(DateTime.Today), 1, 10);

        order.SetStatus(PurchaseOrderStatus.Submitted);
        order.SetStatus(PurchaseOrderStatus.Received);

        Assert.Equal(PurchaseOrderStatus.Received, order.Status);
        Assert.Throws<InvalidOperationException>(() => order.SetStatus(PurchaseOrderStatus.Draft));
    }

    [Fact]
    public void PurchaseOrder_AllowsCancellationBeforeReceipt()
    {
        var order = new PurchaseOrder("PO-CANCEL", Guid.CreateVersion7(), Guid.CreateVersion7(), DateOnly.FromDateTime(DateTime.Today), 1, 10);
        order.SetStatus(PurchaseOrderStatus.Submitted);
        order.SetStatus(PurchaseOrderStatus.Cancelled);

        Assert.Equal(PurchaseOrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void InventoryTransaction_CalculatesSignedQuantity()
    {
        var productId = Guid.CreateVersion7();
        var warehouseId = Guid.CreateVersion7();
        var inbound = new InventoryTransaction(productId, warehouseId, InventoryTransactionKind.Inbound, 25, "INV-1", DateOnly.FromDateTime(DateTime.Today), null);
        var outbound = new InventoryTransaction(productId, warehouseId, InventoryTransactionKind.Outbound, 7, "INV-2", DateOnly.FromDateTime(DateTime.Today), null);

        Assert.Equal(25m, inbound.SignedQuantity);
        Assert.Equal(-7m, outbound.SignedQuantity);
        Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryTransaction(productId, warehouseId, InventoryTransactionKind.Inbound, 0, "INV-3", inbound.OccurredOn, null));
    }

    [Fact]
    public void SalesOrder_EnforcesShippingFlowAndCalculatesAmount()
    {
        var order = new SalesOrder(" SO-1 ", Guid.CreateVersion7(), Guid.CreateVersion7(), DateOnly.FromDateTime(DateTime.Today), 3, 1680);
        order.SetStatus(SalesOrderStatus.Submitted);
        order.SetStatus(SalesOrderStatus.Shipped);

        Assert.Equal("SO-1", order.OrderNo);
        Assert.Equal(5040m, order.Amount);
        Assert.Throws<InvalidOperationException>(() => order.SetStatus(SalesOrderStatus.Draft));
    }

    [Fact]
    public void SalesOrder_AllowsCancellationBeforeShipping()
    {
        var order = new SalesOrder("SO-CANCEL", Guid.CreateVersion7(), Guid.CreateVersion7(), DateOnly.FromDateTime(DateTime.Today), 1, 10);
        order.SetStatus(SalesOrderStatus.Submitted);
        order.SetStatus(SalesOrderStatus.Cancelled);

        Assert.Equal(SalesOrderStatus.Cancelled, order.Status);
    }
}
