namespace VelrixWorkHub.Domain;

public enum ErpSettlementKind { Payable, Receivable }
// Keep the original numeric values stable for persisted records.
public enum ErpSettlementStatus { Active = 0, Voided = 1, PendingApproval = 2, Rejected = 3 }

public sealed class ErpSettlement
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string ReferenceNo { get; private set; } = string.Empty;
    public Guid OrderId { get; private set; }
    public Guid PartyId { get; private set; }
    public ErpSettlementKind Kind { get; private set; }
    public decimal Amount { get; private set; }
    public DateOnly OccurredOn { get; private set; }
    public string Notes { get; private set; } = string.Empty;
    public ErpSettlementStatus Status { get; private set; } = ErpSettlementStatus.Active;
    public string VoidReason { get; private set; } = string.Empty;

    public ErpSettlement(string referenceNo, Guid orderId, Guid partyId, ErpSettlementKind kind, decimal amount, DateOnly occurredOn, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(referenceNo)) throw new ArgumentException("核销流水号不能为空。", nameof(referenceNo));
        if (orderId == Guid.Empty) throw new ArgumentException("必须关联业务订单。", nameof(orderId));
        if (partyId == Guid.Empty) throw new ArgumentException("必须关联往来单位。", nameof(partyId));
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "核销金额必须大于 0。");
        ReferenceNo = referenceNo.Trim(); OrderId = orderId; PartyId = partyId; Kind = kind; Amount = decimal.Round(amount, 2); OccurredOn = occurredOn; Notes = notes?.Trim() ?? string.Empty;
    }

    public static ErpSettlement Restore(Guid id, string referenceNo, Guid orderId, Guid partyId, ErpSettlementKind kind, decimal amount, DateOnly occurredOn, string? notes = null, ErpSettlementStatus status = ErpSettlementStatus.Active, string? voidReason = null)
    {
        var item = new ErpSettlement(referenceNo, orderId, partyId, kind, amount, occurredOn, notes) { Id = id };
        item.Status = status; item.VoidReason = voidReason?.Trim() ?? string.Empty;
        return item;
    }

    public void Void(string reason)
    {
        if (Status == ErpSettlementStatus.Voided) throw new InvalidOperationException("该核销流水已撤销。");
        if (Status == ErpSettlementStatus.PendingApproval) throw new InvalidOperationException("审批中的核销不能直接撤销。");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("请填写撤销原因。", nameof(reason));
        Status = ErpSettlementStatus.Voided; VoidReason = reason.Trim();
    }

    public void MarkPendingApproval()
    {
        if (Status is not (ErpSettlementStatus.Active or ErpSettlementStatus.Rejected)) throw new InvalidOperationException("只有有效或已拒绝核销可以进入审批。");
        Status = ErpSettlementStatus.PendingApproval;
    }

    public void Approve()
    {
        if (Status != ErpSettlementStatus.PendingApproval) throw new InvalidOperationException("只有待审批核销可以通过审批。");
        Status = ErpSettlementStatus.Active;
    }

    public void RejectApproval(string reason = "审批拒绝")
    {
        if (Status != ErpSettlementStatus.PendingApproval) throw new InvalidOperationException("只有待审批核销可以拒绝。");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("请填写审批拒绝原因。", nameof(reason));
        Status = ErpSettlementStatus.Rejected;
        VoidReason = reason.Trim();
    }
}
