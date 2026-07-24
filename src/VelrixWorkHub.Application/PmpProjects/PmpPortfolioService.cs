using VelrixWorkHub.Application.Reports;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmpProjects;

public enum PmpDeliveryHealthStatus { Healthy, Attention, AtRisk }

public sealed record PmpDeliveryHealth(PmpDeliveryHealthStatus Status, string Label, IReadOnlyList<string> Reasons);

public sealed record PmpPortfolioProject(
    PmpProject Project,
    int PhaseCount,
    int OverdueMilestoneCount,
    int WbsTaskCount,
    int IncompleteWbsTaskCount,
    int OpenIssueCount,
    int OverdueIssueCount,
    decimal SchedulePerformanceIndex,
    decimal ActiveContractAmount,
    decimal ContractedOrderAmount,
    decimal UnorderedContractAmount,
    bool HasProjectScopedOrders,
    int SalesOrderCount,
    int ShippedOrderCount,
    int PendingShipmentOrderCount,
    decimal SalesOrderAmount,
    decimal ShippedOrderAmount,
    decimal PendingShipmentAmount,
    decimal SalesOrderQuantity,
    decimal ShippedOrderQuantity,
    decimal PendingShipmentQuantity,
    decimal ReceivableAmount,
    PmpDeliveryHealth DeliveryHealth)
{
    public decimal FulfillmentRate => SalesOrderAmount <= 0 ? 0 : decimal.Round(ShippedOrderAmount / SalesOrderAmount * 100, 1);
}

public sealed record PmpPortfolioSummary(
    int ProjectCount,
    int ActiveProjectCount,
    int PhaseCount,
    int OverdueMilestoneCount,
    int OpenIssueCount,
    int OverdueIssueCount,
    int IncompleteWbsTaskCount,
    decimal AverageSchedulePerformanceIndex,
    decimal ActiveContractAmount,
    decimal UnorderedContractAmount,
    decimal ReceivableAmount,
    int AtRiskProjectCount,
    int AttentionProjectCount,
    IReadOnlyList<PmpPortfolioProject> Projects,
    decimal PendingShipmentAmount,
    int PendingShipmentOrderCount,
    decimal PendingShipmentQuantity);

public static class PmpPortfolioService
{
    public static PmpPortfolioSummary Build(DateOnly today, IEnumerable<PmpProject> projects, IEnumerable<PmpProjectPhase> phases, IEnumerable<PmpWbsTask> wbsTasks, IEnumerable<PmpProjectIssue> issues, IEnumerable<PmpEvmSnapshot> evmSnapshots, IEnumerable<SalesOrder> salesOrders, IEnumerable<ErpSettlement> settlements, IEnumerable<SalesContract>? contracts = null)
    {
        var projectArray = projects.ToArray();
        var phaseArray = phases.ToArray();
        var taskArray = wbsTasks.ToArray();
        var issueArray = issues.ToArray();
        var evmArray = evmSnapshots.ToArray();
        var contractArray = contracts?.ToArray() ?? [];
        var salesOrderArray = salesOrders.ToArray();
        var projectItems = projectArray.Select(project => BuildProject(today, project, phaseArray, taskArray, issueArray, evmArray, salesOrderArray, settlements, contractArray)).OrderByDescending(x => x.DeliveryHealth.Status).ThenByDescending(x => x.OverdueMilestoneCount).ThenByDescending(x => x.OverdueIssueCount).ThenByDescending(x => x.OpenIssueCount).ThenBy(x => x.Project.Code).ToArray();
        var spiValues = projectItems.Where(x => x.SchedulePerformanceIndex > 0).Select(x => x.SchedulePerformanceIndex).ToArray();
        return new PmpPortfolioSummary(projectItems.Length, projectItems.Count(x => x.Project.Status == PmpProjectStatus.Active), projectItems.Sum(x => x.PhaseCount), projectItems.Sum(x => x.OverdueMilestoneCount), projectItems.Sum(x => x.OpenIssueCount), projectItems.Sum(x => x.OverdueIssueCount), projectItems.Sum(x => x.IncompleteWbsTaskCount), spiValues.Length == 0 ? 0 : decimal.Round(spiValues.Average(), 2), projectItems.Sum(x => x.ActiveContractAmount), projectItems.Sum(x => x.UnorderedContractAmount), projectItems.Sum(x => x.ReceivableAmount), projectItems.Count(x => x.DeliveryHealth.Status == PmpDeliveryHealthStatus.AtRisk), projectItems.Count(x => x.DeliveryHealth.Status == PmpDeliveryHealthStatus.Attention), projectItems, projectItems.Sum(x => x.PendingShipmentAmount), projectItems.Sum(x => x.PendingShipmentOrderCount), projectItems.Sum(x => x.PendingShipmentQuantity));
    }

    private static PmpPortfolioProject BuildProject(DateOnly today, PmpProject project, IReadOnlyList<PmpProjectPhase> phases, IReadOnlyList<PmpWbsTask> tasks, IReadOnlyList<PmpProjectIssue> issues, IReadOnlyList<PmpEvmSnapshot> evmSnapshots, IEnumerable<SalesOrder> salesOrders, IEnumerable<ErpSettlement> settlements, IReadOnlyList<SalesContract> contracts)
    {
        var projectPhases = phases.Where(x => x.ProjectId == project.Id).ToArray();
        var overdueMilestones = projectPhases.Count(x => x.Kind == PmpProjectPhaseKind.Milestone && x.Status is not PmpProjectPhaseStatus.Completed and not PmpProjectPhaseStatus.Cancelled && x.PlannedEnd < today);
        var projectTasks = tasks.Where(x => x.ProjectId == project.Id).ToArray();
        var openIssues = issues.Where(x => x.ProjectId == project.Id && x.Status is not PmpProjectIssueStatus.Resolved and not PmpProjectIssueStatus.Closed).ToArray();
        var evm = evmSnapshots.FirstOrDefault(x => x.Project.Id == project.Id);
        var commercial = PmpProjectCommercialInsightService.Build(project, salesOrders, settlements, contracts);
        var overdueIssues = openIssues.Count(x => x.DueDate is DateOnly dueDate && dueDate < today);
        var incompleteTasks = projectTasks.Count(x => x.Status != PmpWbsTaskStatus.Done);
        var health = BuildDeliveryHealth(overdueMilestones, overdueIssues, openIssues.Length, incompleteTasks, evm, commercial.UnorderedContractAmount);
        return new PmpPortfolioProject(project, projectPhases.Length, overdueMilestones, projectTasks.Length, incompleteTasks, openIssues.Length, overdueIssues, evm?.SchedulePerformanceIndex ?? 0, commercial.ActiveContractAmount, commercial.ContractedOrderAmount, commercial.UnorderedContractAmount, commercial.HasProjectScopedOrders, commercial.SalesOrderCount, commercial.ShippedOrderCount, commercial.PendingShipmentOrderCount, commercial.SalesOrderAmount, commercial.ShippedOrderAmount, commercial.PendingShipmentAmount, commercial.SalesOrderQuantity, commercial.ShippedOrderQuantity, commercial.PendingShipmentQuantity, commercial.ReceivableAmount, health);
    }

    private static PmpDeliveryHealth BuildDeliveryHealth(int overdueMilestones, int overdueIssues, int openIssues, int incompleteTasks, PmpEvmSnapshot? evm, decimal unorderedContractAmount)
    {
        var reasons = new List<string>();
        if (overdueMilestones > 0) reasons.Add($"逾期里程碑 {overdueMilestones} 项");
        if (overdueIssues > 0) reasons.Add($"逾期风险问题 {overdueIssues} 项");
        if (evm is not null && evm.PlannedValue > 0 && evm.SchedulePerformanceIndex > 0 && evm.SchedulePerformanceIndex < 0.8m) reasons.Add($"SPI {evm.SchedulePerformanceIndex:0.00} 低于 0.80");
        if (openIssues > 0) reasons.Add($"未关闭风险问题 {openIssues} 项");
        if (incompleteTasks > 0) reasons.Add($"未完成 WBS {incompleteTasks} 项");
        if (unorderedContractAmount > 0) reasons.Add($"待承接合同 ¥{unorderedContractAmount:N2}");

        var status = overdueMilestones > 0 || overdueIssues > 0 || reasons.Any(x => x.StartsWith("SPI", StringComparison.Ordinal))
            ? PmpDeliveryHealthStatus.AtRisk
            : reasons.Count == 0 ? PmpDeliveryHealthStatus.Healthy : PmpDeliveryHealthStatus.Attention;
        var label = status switch
        {
            PmpDeliveryHealthStatus.Healthy => "健康",
            PmpDeliveryHealthStatus.Attention => "需关注",
            PmpDeliveryHealthStatus.AtRisk => "高风险",
            _ => status.ToString()
        };
        return new PmpDeliveryHealth(status, label, reasons);
    }
}
