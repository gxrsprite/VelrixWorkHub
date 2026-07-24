using VelrixWorkHub.Domain;
using VelrixWorkHub.Application.Workflow;

namespace VelrixWorkHub.Application.Assets;

public interface IOaConsumableSupplyRepository
{
    IReadOnlyList<OaConsumableSupply> List();
    OaConsumableSupply? Get(Guid id);
    void Add(OaConsumableSupply supply);
    void Update(OaConsumableSupply supply);
}

public interface IOaConsumableTransactionRepository
{
    IReadOnlyList<OaConsumableTransaction> List(Guid? supplyId = null, Guid? recipientUserId = null);
    void Add(OaConsumableTransaction transaction);
}

public sealed record OaConsumableBalance(Guid SupplyId, decimal Quantity);

public sealed class ConsumableSupplyService(IOaConsumableSupplyRepository supplies, IOaConsumableTransactionRepository transactions,
    IWorkflowTransactionBoundary? transactionBoundary = null)
{
    public IReadOnlyList<OaConsumableSupply> List() => supplies.List().OrderBy(item => item.Code).ToArray();
    public IReadOnlyList<OaConsumableBalance> Balances() => transactions.List().GroupBy(item => item.SupplyId)
        .Select(group => new OaConsumableBalance(group.Key, group.Sum(item => item.SignedQuantity))).OrderBy(item => item.SupplyId).ToArray();
    public IReadOnlyList<OaConsumableTransaction> ListTransactions(Guid? supplyId = null, Guid? recipientUserId = null)
        => transactions.List(supplyId, recipientUserId).OrderByDescending(item => item.OccurredAt).ThenByDescending(item => item.Id).ToArray();
    public decimal BalanceOf(Guid supplyId) => Balances().FirstOrDefault(item => item.SupplyId == supplyId)?.Quantity ?? 0m;

    public OaConsumableSupply Create(string code, string name, string unit, string? location, string? otherInfo, bool canManage)
    {
        Ensure(canManage);
        if (supplies.List().Any(item => item.Code.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("办公用品编码已存在。");
        var supply = new OaConsumableSupply(code, name, unit, location, otherInfo, DateTime.Now);
        supplies.Add(supply);
        return supply;
    }

    public OaConsumableTransaction Receive(Guid supplyId, decimal quantity, string sourceNo, string actorName, string? notes, bool canManage)
        => Record(supplyId, OaConsumableTransactionKind.Inbound, quantity, null, sourceNo, actorName, notes, canManage);

    public OaConsumableTransaction Issue(Guid supplyId, Guid recipientUserId, decimal quantity, string sourceNo, string actorName, string? notes, bool canManage)
    {
        Ensure(canManage);
        if (BalanceOf(supplyId) < quantity) throw new InvalidOperationException($"办公用品库存不足，当前可用 {BalanceOf(supplyId):N2}。");
        return Record(supplyId, OaConsumableTransactionKind.Issued, quantity, recipientUserId, sourceNo, actorName, notes, true);
    }

    private OaConsumableTransaction Record(Guid supplyId, OaConsumableTransactionKind kind, decimal quantity, Guid? recipientUserId,
        string sourceNo, string actorName, string? notes, bool canManage)
    {
        Ensure(canManage);
        var supply = supplies.Get(supplyId) ?? throw new InvalidOperationException("办公用品不存在。");
        if (!supply.IsActive) throw new InvalidOperationException("办公用品已停用，不能登记流水。");
        if (transactions.List().Any(item => item.SourceNo.Equals(sourceNo.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("办公用品流水来源单号已存在。");
        var transaction = new OaConsumableTransaction(supplyId, kind, quantity, recipientUserId, sourceNo, actorName, notes, DateTime.Now);
        if (transactionBoundary is null) transactions.Add(transaction); else transactionBoundary.Execute(() => transactions.Add(transaction));
        return transaction;
    }

    private static void Ensure(bool canManage) { if (!canManage) throw new UnauthorizedAccessException("当前用户没有维护办公用品库存的权限。"); }
}
