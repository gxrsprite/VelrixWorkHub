using VelrixWorkHub.Application.PmpProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PmpPortfolioServiceTests
{
    [Fact]
    public void Build_AggregatesWbsIssuesEvmAndReceivables()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var customer = new Customer("Aster 科技");
        var product = new Product("SKU-PORTFOLIO", "项目服务", "件", 100, null);
        var project = new PmpProject("PRJ-PORTFOLIO", "组合测试项目", customer.Id, "项目经理", today.AddDays(-5), today.AddDays(20));
        project.SetStatus(PmpProjectStatus.Active);
        var doneTask = new PmpWbsTask(project.Id, null, "已完成工作包", "成员 A", 1, today.AddDays(-3), today.AddDays(-2), false);
        doneTask.SetPercentComplete(100);
        var pendingTask = new PmpWbsTask(project.Id, null, "待完成工作包", "成员 B", 2, today, today.AddDays(2), false);
        var overdueMilestone = new PmpProjectPhase(project.Id, "逾期里程碑", PmpProjectPhaseKind.Milestone, 1, today.AddDays(-1), today.AddDays(-1));
        var overdueIssue = new PmpProjectIssue(project.Id, PmpProjectIssueKind.Risk, "逾期风险", null, "项目经理", PmpProjectIssuePriority.High, today.AddDays(-1));
        var closedIssue = new PmpProjectIssue(project.Id, PmpProjectIssueKind.Issue, "已关闭问题", null, null, PmpProjectIssuePriority.Low, today.AddDays(-1));
        closedIssue.SetStatus(PmpProjectIssueStatus.Closed);
        var evm = new PmpEvmSnapshot(project, null, 100, 80, 100);
        var activeContract = new SalesContract(customer.Id, null, "CT-PORTFOLIO", "组合测试合同", 300, today.AddDays(-1), today.AddDays(30));
        activeContract.Activate();
        var order = new SalesOrder("SO-PORTFOLIO", customer.Id, product.Id, today, 2, 100, activeContract.Id);
        var receipt = new ErpSettlement("REC-PORTFOLIO", order.Id, customer.Id, ErpSettlementKind.Receivable, 50, today);
        var terminatedContract = new SalesContract(customer.Id, null, "CT-PORTFOLIO-OLD", "历史合同", 100, today.AddDays(-30), today.AddDays(-1));
        terminatedContract.Activate();
        terminatedContract.Terminate();

        var result = PmpPortfolioService.Build(today, new[] { project }, new[] { overdueMilestone }, new[] { doneTask, pendingTask }, new[] { overdueIssue, closedIssue }, new[] { evm }, new[] { order }, new[] { receipt }, new[] { activeContract, terminatedContract });
        var item = Assert.Single(result.Projects);

        Assert.Equal(1, result.ProjectCount);
        Assert.Equal(1, result.ActiveProjectCount);
        Assert.Equal(1, result.PhaseCount);
        Assert.Equal(1, result.OverdueMilestoneCount);
        Assert.Equal(1, result.OpenIssueCount);
        Assert.Equal(1, result.OverdueIssueCount);
        Assert.Equal(1, result.IncompleteWbsTaskCount);
        Assert.Equal(0.8m, result.AverageSchedulePerformanceIndex);
        Assert.Equal(1, item.SalesOrderCount);
        Assert.Equal(0, item.ShippedOrderCount);
        Assert.Equal(1, item.PendingShipmentOrderCount);
        Assert.Equal(200m, item.SalesOrderAmount);
        Assert.Equal(0m, item.ShippedOrderAmount);
        Assert.Equal(200m, item.PendingShipmentAmount);
        Assert.Equal(2m, item.SalesOrderQuantity);
        Assert.Equal(0m, item.ShippedOrderQuantity);
        Assert.Equal(2m, item.PendingShipmentQuantity);
        Assert.Equal(0m, item.FulfillmentRate);
        Assert.Equal(1, result.PendingShipmentOrderCount);
        Assert.Equal(200m, result.PendingShipmentAmount);
        Assert.Equal(2m, result.PendingShipmentQuantity);
        Assert.Equal(300m, result.ActiveContractAmount);
        Assert.Equal(100m, result.UnorderedContractAmount);
        Assert.Equal(300m, item.ActiveContractAmount);
        Assert.Equal(200m, item.ContractedOrderAmount);
        Assert.Equal(100m, item.UnorderedContractAmount);
        Assert.Equal(150m, result.ReceivableAmount);
        Assert.Equal(150m, item.ReceivableAmount);
        Assert.Equal(1, result.AtRiskProjectCount);
        Assert.Equal(0, result.AttentionProjectCount);
        Assert.Equal(PmpDeliveryHealthStatus.AtRisk, item.DeliveryHealth.Status);
        Assert.Contains("逾期里程碑", string.Join("、", item.DeliveryHealth.Reasons));
    }

    [Fact]
    public void Build_SeparatesOrdersByProjectWhenAssignmentsExist()
    {
        var today = DateOnly.FromDateTime(DateTime.Today); var customer = new Customer("Aster 科技"); var product = new Product("SKU-PMP-SCOPE", "项目服务", "件", 100m, null);
        var first = new PmpProject("PRJ-SCOPE-01", "项目一", customer.Id, null, today, today.AddDays(30));
        var second = new PmpProject("PRJ-SCOPE-02", "项目二", customer.Id, null, today, today.AddDays(30));
        var contract = new SalesContract(customer.Id, null, "CT-SCOPE-01", "项目合同", 1000m, today, today.AddDays(30)); contract.Activate();
        var firstOrder = new SalesOrder("SO-SCOPE-01", customer.Id, product.Id, today, 3, 100m, contract.Id, first.Id);
        var secondOrder = new SalesOrder("SO-SCOPE-02", customer.Id, product.Id, today, 1, 100m, contract.Id, second.Id);
        var unassignedOrder = new SalesOrder("SO-SCOPE-03", customer.Id, product.Id, today, 1, 500m);
        var firstReceipt = new ErpSettlement("REC-SCOPE-01", firstOrder.Id, customer.Id, ErpSettlementKind.Receivable, 50m, today);
        var unassignedReceipt = new ErpSettlement("REC-SCOPE-02", unassignedOrder.Id, customer.Id, ErpSettlementKind.Receivable, 500m, today);

        var result = PmpPortfolioService.Build(today, [first, second], [], [], [], [], [firstOrder, secondOrder, unassignedOrder], [firstReceipt, unassignedReceipt], [contract]);

        var firstItem = result.Projects.Single(x => x.Project.Id == first.Id);
        var secondItem = result.Projects.Single(x => x.Project.Id == second.Id);
        Assert.Equal(1, firstItem.SalesOrderCount);
        Assert.Equal(300m, firstItem.ContractedOrderAmount);
        Assert.Equal(250m, firstItem.ReceivableAmount);
        Assert.Equal(1, secondItem.SalesOrderCount);
        Assert.Equal(100m, secondItem.ContractedOrderAmount);
        Assert.Equal(100m, secondItem.ReceivableAmount);
        Assert.True(firstItem.HasProjectScopedOrders);
        Assert.True(secondItem.HasProjectScopedOrders);
    }

    [Fact]
    public void Build_ClassifiesOpenExecutionSignalsAsAttentionAndEmptyProjectAsHealthy()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var attentionProject = new PmpProject("PRJ-HEALTH-ATTN", "需关注项目", null, null, today, today.AddDays(30));
        var openIssue = new PmpProjectIssue(attentionProject.Id, PmpProjectIssueKind.Risk, "待确认风险", null, null, PmpProjectIssuePriority.Medium, today.AddDays(5));
        var healthyProject = new PmpProject("PRJ-HEALTHY", "健康项目", null, null, today, today.AddDays(30));

        var result = PmpPortfolioService.Build(today, [attentionProject, healthyProject], [], [], [openIssue], [], [], []);

        Assert.Equal(1, result.AttentionProjectCount);
        Assert.Equal(0, result.AtRiskProjectCount);
        Assert.Equal(PmpDeliveryHealthStatus.Attention, result.Projects[0].DeliveryHealth.Status);
        Assert.Contains("未关闭风险问题", string.Join("、", result.Projects[0].DeliveryHealth.Reasons));
        Assert.Equal(PmpDeliveryHealthStatus.Healthy, result.Projects[1].DeliveryHealth.Status);
        Assert.Empty(result.Projects[1].DeliveryHealth.Reasons);
    }
}
