using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class SalesOrderContractTests
{
    [Fact]
    public void Restore_PreservesContractReference()
    {
        var customer = Guid.NewGuid(); var contract = Guid.NewGuid(); var project = Guid.NewGuid();
        var order = SalesOrder.Restore(Guid.NewGuid(), "SO-CONTRACT-01", customer, Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today), 1, 100m, SalesOrderStatus.Draft, contract, project);

        Assert.Equal(contract, order.ContractId);
        Assert.Equal(project, order.PmpProjectId);
        Assert.Equal(customer, order.CustomerId);
    }
}
