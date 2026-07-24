using VelrixWorkHub.Application.Assets;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class ConsumableSupplyServiceTests
{
    [Fact]
    public void ReceiveAndIssue_TracksBalanceAndRecipientAudit()
    {
        var supplies = new SupplyRepository(); var transactions = new TransactionRepository();
        var service = new ConsumableSupplyService(supplies, transactions);
        var supply = service.Create("SUP-A4", "A4 打印纸", "包", "行政库", "{}", true);
        var recipient = Guid.CreateVersion7();

        service.Receive(supply.Id, 10, "SUP-IN-001", "admin", "采购入库", true);
        var issued = service.Issue(supply.Id, recipient, 3, "SUP-OUT-001", "admin", "研发领用", true);

        Assert.Equal(7, service.BalanceOf(supply.Id));
        Assert.Equal(recipient, issued.RecipientUserId);
        Assert.Equal(-3, issued.SignedQuantity);
        Assert.Single(service.ListTransactions(recipientUserId: recipient));
    }

    [Fact]
    public void Issue_RejectsInsufficientDuplicateAndUnauthorizedWrites()
    {
        var supplies = new SupplyRepository(); var transactions = new TransactionRepository();
        var service = new ConsumableSupplyService(supplies, transactions);
        var supply = service.Create("SUP-PEN", "签字笔", "支", null, null, true);

        Assert.Throws<UnauthorizedAccessException>(() => service.Receive(supply.Id, 1, "SUP-IN-002", "admin", null, false));
        service.Receive(supply.Id, 1, "SUP-IN-002", "admin", null, true);
        Assert.Throws<InvalidOperationException>(() => service.Issue(supply.Id, Guid.CreateVersion7(), 2, "SUP-OUT-002", "admin", null, true));
        Assert.Throws<InvalidOperationException>(() => service.Receive(supply.Id, 1, "SUP-IN-002", "admin", null, true));
    }

    private sealed class SupplyRepository : IOaConsumableSupplyRepository
    {
        private readonly List<OaConsumableSupply> items = [];
        public IReadOnlyList<OaConsumableSupply> List() => items;
        public OaConsumableSupply? Get(Guid id) => items.FirstOrDefault(item => item.Id == id);
        public void Add(OaConsumableSupply supply) => items.Add(supply);
        public void Update(OaConsumableSupply supply) { }
    }

    private sealed class TransactionRepository : IOaConsumableTransactionRepository
    {
        private readonly List<OaConsumableTransaction> items = [];
        public IReadOnlyList<OaConsumableTransaction> List(Guid? supplyId = null, Guid? recipientUserId = null)
            => items.Where(item => (!supplyId.HasValue || item.SupplyId == supplyId) && (!recipientUserId.HasValue || item.RecipientUserId == recipientUserId)).ToArray();
        public void Add(OaConsumableTransaction transaction) => items.Add(transaction);
    }
}
