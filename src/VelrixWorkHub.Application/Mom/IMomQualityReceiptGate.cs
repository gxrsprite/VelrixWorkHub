namespace VelrixWorkHub.Application.Mom;

public interface IMomQualityReceiptGate
{
    void EnsureCanReceive(Guid purchaseOrderId, Guid productId);
}
