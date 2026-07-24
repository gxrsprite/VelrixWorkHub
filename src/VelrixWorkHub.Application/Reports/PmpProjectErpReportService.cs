using VelrixWorkHub.Application.PmpProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Reports;

public sealed record PmpProjectErpReportRow(
    PmpProject Project,
    PmpProjectCommercialInsight Commercial)
{
    public bool HasCommercialData =>
        Commercial.SalesOrderCount > 0 ||
        Commercial.ActiveContractAmount > 0 ||
        Commercial.ReceivedAmount > 0;
}

public static class PmpProjectErpReportService
{
    public static IReadOnlyList<PmpProjectErpReportRow> Build(
        IEnumerable<PmpProject> projects,
        IEnumerable<SalesOrder> salesOrders,
        IEnumerable<ErpSettlement> settlements,
        IEnumerable<SalesContract> contracts)
    {
        var orderSnapshot = salesOrders.ToArray();
        var settlementSnapshot = settlements.ToArray();
        var contractSnapshot = contracts.ToArray();

        return projects
            .Select(project => new PmpProjectErpReportRow(
                project,
                PmpProjectCommercialInsightService.Build(project, orderSnapshot, settlementSnapshot, contractSnapshot)))
            .Where(x => x.HasCommercialData)
            .OrderByDescending(x => x.Commercial.ReceivableAmount)
            .ThenBy(x => x.Project.Code)
            .ToArray();
    }
}
