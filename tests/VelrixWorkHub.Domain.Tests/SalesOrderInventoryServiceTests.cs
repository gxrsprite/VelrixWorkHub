using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class SalesOrderInventoryServiceTests
{
    [Fact]
    public void Get_SeparatesPhysicalCommittedAndDraftQuantities()
    {
        var customer = Guid.CreateVersion7();
        var product = Guid.CreateVersion7();
        var draft = new SalesOrder("SO-DRAFT", customer, product, Today, 4, 10);
        var submitted = new SalesOrder("SO-SUBMITTED", customer, product, Today, 3, 10);
        submitted.SetStatus(SalesOrderStatus.Submitted);
        var shipped = new SalesOrder("SO-SHIPPED", customer, product, Today, 2, 10);
        shipped.SetStatus(SalesOrderStatus.Submitted);
        shipped.SetStatus(SalesOrderStatus.Shipped);
        var cancelled = new SalesOrder("SO-CANCELLED", customer, product, Today, 5, 10);
        cancelled.SetStatus(SalesOrderStatus.Cancelled);

        var result = SalesOrderInventoryService.Get(product, new[] { new InventoryBalance(product, Guid.CreateVersion7(), 10) }, new[] { draft, submitted, shipped, cancelled });

        Assert.Equal(10m, result.OnHandQuantity);
        Assert.Equal(3m, result.SubmittedQuantity);
        Assert.Equal(3m, result.FrozenQuantity);
        Assert.Equal(4m, result.DraftQuantity);
        Assert.Equal(7m, result.AvailableQuantity);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
}
