using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.SalesOrders;

/// <summary>
/// Application-facing sales-order status gate for fulfillment modules.
/// The sales-order module remains the owner of the Submitted -> Shipped transition.
/// </summary>
public interface ISalesOrderShipmentService
{
    void ConfirmShipped(SalesOrder item);
    void RestoreSubmittedAfterRollback(SalesOrder item);
}
