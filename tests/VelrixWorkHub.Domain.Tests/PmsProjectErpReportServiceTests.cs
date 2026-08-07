using VelrixWorkHub.Application.Reports;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PmsProjectErpReportServiceTests
{
    [Fact]
    public void Build_ReportsProjectCommercialTotalsAndExcludesEmptyProjects()
    {
        var customer = Guid.NewGuid();
        var otherCustomer = Guid.NewGuid();
        var product = Guid.NewGuid();
        var project = new PmsProject("PRJ-REPORT-001", "交付项目", customer, "项目经理", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var emptyProject = new PmsProject("PRJ-REPORT-002", "空项目", otherCustomer, "项目经理", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var contract = new SalesContract(customer, null, "CT-REPORT-001", "交付合同", 1000, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        contract.Activate();
        var order = new SalesOrder("SO-REPORT-001", customer, product, new DateOnly(2026, 2, 1), 1, 600, contract.Id, project.Id);
        var settlement = new ErpSettlement("REC-REPORT-001", order.Id, customer, ErpSettlementKind.Receivable, 100, new DateOnly(2026, 2, 2));

        var result = PmsProjectErpReportService.Build([project, emptyProject], [order], [settlement], [contract]);

        var row = Assert.Single(result);
        Assert.Equal(project.Id, row.Project.Id);
        Assert.Equal(600, row.Commercial.SalesOrderAmount);
        Assert.Equal(100, row.Commercial.ReceivedAmount);
        Assert.Equal(500, row.Commercial.ReceivableAmount);
        Assert.Equal(1000, row.Commercial.ActiveContractAmount);
        Assert.Equal(600, row.Commercial.ContractedOrderAmount);
        Assert.Equal(400, row.Commercial.UnorderedContractAmount);
    }
}
