using VelrixWorkHub.Application.PurchaseOrders;
using VelrixWorkHub.Application.SalesOrders;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Settlements;

public sealed record SettlementOrderBalance(Guid OrderId, string OrderNo, ErpSettlementKind Kind, decimal OrderAmount, decimal SettledAmount)
{
    public decimal PendingAmount { get; init; }
    public DateOnly? DueDate { get; init; }
    public decimal RemainingAmount => decimal.Round(OrderAmount - SettledAmount, 2);
    public decimal AvailableAmount => decimal.Round(Math.Max(0, RemainingAmount - PendingAmount), 2);
}

public sealed class SettlementService(ISettlementRepository repository, IPurchaseOrderRepository purchaseOrders, ISalesOrderRepository salesOrders)
{
    public IReadOnlyList<ErpSettlement> List(ErpSettlementKind? kind = null, ErpSettlementStatus? status = null, string? keyword = null, Guid? partyId = null)
    {
        keyword = keyword?.Trim();
        return repository.List()
            .Where(x => kind is null || x.Kind == kind)
            .Where(x => status is null || x.Status == status)
            .Where(x => partyId is null || x.PartyId == partyId)
            .Where(x => string.IsNullOrWhiteSpace(keyword) || x.ReferenceNo.Contains(keyword, StringComparison.OrdinalIgnoreCase) || x.Notes.Contains(keyword, StringComparison.OrdinalIgnoreCase) || x.VoidReason.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.ReferenceNo).ToArray();
    }

    public IReadOnlyList<SettlementOrderBalance> OrderBalances(ErpSettlementKind kind)
    {
        var settled = repository.List().Where(x => x.Kind == kind && x.Status == ErpSettlementStatus.Active).GroupBy(x => x.OrderId).ToDictionary(x => x.Key, x => x.Sum(item => item.Amount));
        var pending = repository.List().Where(x => x.Kind == kind && x.Status == ErpSettlementStatus.PendingApproval).GroupBy(x => x.OrderId).ToDictionary(x => x.Key, x => x.Sum(item => item.Amount));
        return kind == ErpSettlementKind.Payable
            ? purchaseOrders.List().Where(x => x.Status != PurchaseOrderStatus.Cancelled).Select(x => new SettlementOrderBalance(x.Id, x.OrderNo, kind, x.Amount, settled.GetValueOrDefault(x.Id)) { PendingAmount = pending.GetValueOrDefault(x.Id), DueDate = x.DueDate }).Where(x => x.RemainingAmount > 0).OrderBy(x => x.OrderNo).ToArray()
            : salesOrders.List().Where(x => x.Status != SalesOrderStatus.Cancelled).Select(x => new SettlementOrderBalance(x.Id, x.OrderNo, kind, x.Amount, settled.GetValueOrDefault(x.Id)) { PendingAmount = pending.GetValueOrDefault(x.Id), DueDate = x.DueDate }).Where(x => x.RemainingAmount > 0).OrderBy(x => x.OrderNo).ToArray();
    }

    public SettlementOrderBalance? GetOrderBalance(ErpSettlementKind kind, Guid orderId)
    {
        var matching = repository.List().Where(x => x.Kind == kind && x.OrderId == orderId).ToArray();
        var settledAmount = matching.Where(x => x.Status == ErpSettlementStatus.Active).Sum(x => x.Amount);
        var pendingAmount = matching.Where(x => x.Status == ErpSettlementStatus.PendingApproval).Sum(x => x.Amount);
        return kind == ErpSettlementKind.Payable
            ? purchaseOrders.List().Where(x => x.Id == orderId).Select(x => new SettlementOrderBalance(x.Id, x.OrderNo, kind, x.Amount, settledAmount) { PendingAmount = pendingAmount, DueDate = x.DueDate }).FirstOrDefault()
            : salesOrders.List().Where(x => x.Id == orderId).Select(x => new SettlementOrderBalance(x.Id, x.OrderNo, kind, x.Amount, settledAmount) { PendingAmount = pendingAmount, DueDate = x.DueDate }).FirstOrDefault();
    }

    public ErpSettlement Create(ErpSettlementKind kind, Guid orderId, decimal amount, string referenceNo, DateOnly occurredOn, string? notes = null)
    {
        var item = CreateCore(kind, orderId, amount, referenceNo, occurredOn, notes);
        repository.Add(item);
        return item;
    }

    public ErpSettlement CreatePendingApproval(ErpSettlementKind kind, Guid orderId, decimal amount, string referenceNo, DateOnly occurredOn, string? notes = null)
    {
        var item = CreateCore(kind, orderId, amount, referenceNo, occurredOn, notes);
        item.MarkPendingApproval();
        repository.Add(item);
        return item;
    }

    public void Approve(Guid settlementId)
    {
        var item = repository.List().FirstOrDefault(x => x.Id == settlementId) ?? throw new InvalidOperationException("核销流水不存在或已被删除。");
        if (item.Status == ErpSettlementStatus.Active) return;
        item.Approve();
        repository.Update(item);
    }

    public void SubmitForApproval(Guid settlementId)
    {
        var item = repository.List().FirstOrDefault(x => x.Id == settlementId) ?? throw new InvalidOperationException("核销流水不存在或已被删除。");
        if (item.Status != ErpSettlementStatus.Rejected) throw new InvalidOperationException("只有审批拒绝的核销可以重新提交。");
        EnsurePendingCapacity(item);
        item.MarkPendingApproval();
        repository.Update(item);
    }

    public void RejectApproval(Guid settlementId, string reason = "审批拒绝")
    {
        var item = repository.List().FirstOrDefault(x => x.Id == settlementId) ?? throw new InvalidOperationException("核销流水不存在或已被删除。");
        if (item.Status != ErpSettlementStatus.PendingApproval) return;
        item.RejectApproval(reason);
        repository.Update(item);
    }

    public void Void(Guid settlementId, string reason)
    {
        var item = repository.List().FirstOrDefault(x => x.Id == settlementId) ?? throw new InvalidOperationException("核销流水不存在或已被删除。");
        item.Void(reason);
        repository.Update(item);
    }

    private ErpSettlement CreateCore(ErpSettlementKind kind, Guid orderId, decimal amount, string referenceNo, DateOnly occurredOn, string? notes)
    {
        if (repository.List().Any(x => x.ReferenceNo.Equals(referenceNo.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("核销流水号已存在。");
        var balance = FindBalance(kind, orderId) ?? throw new InvalidOperationException("只能核销有效的采购或销售订单。");
        var remaining = balance.AvailableAmount;
        if (amount > remaining) throw new InvalidOperationException($"核销金额不能超过可用剩余金额 {remaining:N2}。");
        var partyId = kind == ErpSettlementKind.Payable ? purchaseOrders.List().First(x => x.Id == orderId).SupplierId : salesOrders.List().First(x => x.Id == orderId).CustomerId;
        return new ErpSettlement(referenceNo, orderId, partyId, kind, amount, occurredOn, notes);
    }

    private void EnsurePendingCapacity(ErpSettlement item)
    {
        var balance = FindBalance(item.Kind, item.OrderId) ?? throw new InvalidOperationException("只能核销有效的采购或销售订单。");
        var remaining = balance.AvailableAmount;
        if (item.Amount > remaining) throw new InvalidOperationException($"核销金额不能超过当前可用剩余金额 {remaining:N2}，请先处理其他待审批流水。");
    }

    private SettlementOrderBalance? FindBalance(ErpSettlementKind kind, Guid orderId) => OrderBalances(kind).FirstOrDefault(x => x.OrderId == orderId);
}
