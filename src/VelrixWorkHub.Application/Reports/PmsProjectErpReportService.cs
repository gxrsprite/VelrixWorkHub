using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Reports;

public sealed record PmsProjectErpReportRow(
    PmsProject Project,
    PmsProjectCommercialInsight Commercial)
{
    public bool HasCommercialData =>
        Commercial.SalesOrderCount > 0 ||
        Commercial.ActiveContractAmount > 0 ||
        Commercial.ReceivedAmount > 0;
}

public static class PmsProjectErpReportService
{
    public static IReadOnlyList<PmsProjectErpReportRow> Build(
        IEnumerable<PmsProject> projects,
        IEnumerable<SalesOrder> salesOrders,
        IEnumerable<ErpSettlement> settlements,
        IEnumerable<SalesContract> contracts)
    {
        var orderSnapshot = salesOrders.ToArray();
        var settlementSnapshot = settlements.ToArray();
        var contractSnapshot = contracts.ToArray();

        return projects
            .Select(project => new PmsProjectErpReportRow(
                project,
                PmsProjectCommercialInsightService.Build(project, orderSnapshot, settlementSnapshot, contractSnapshot)))
            .Where(x => x.HasCommercialData)
            .OrderByDescending(x => x.Commercial.ReceivableAmount)
            .ThenBy(x => x.Project.Code)
            .ToArray();
    }
}
