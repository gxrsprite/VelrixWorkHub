using VelrixWorkHub.Application.Contracts;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class SalesContractTests
{
    [Fact]
    public void ContractRequiresValidDatesAndCanChangeStatus()
    {
        Assert.Throws<ArgumentException>(() => new SalesContract(Guid.CreateVersion7(), null, "CT-1", "服务", 1, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(-1))));
        var contract = new SalesContract(Guid.CreateVersion7(), null, "CT-1", "服务", 1, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        contract.Activate();
        contract.Terminate();
        Assert.Equal(ContractStatus.Terminated, contract.Status);
    }

    [Fact]
    public void TerminatedContractCannotReactivate()
    {
        var contract = new SalesContract(Guid.CreateVersion7(), null, "CT-1", "服务", 1, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        contract.Activate();
        contract.Terminate();

        Assert.Throws<InvalidOperationException>(() => contract.Activate());
    }
}
