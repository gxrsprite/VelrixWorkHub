using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.SalesOrders;
public interface ISalesOrderRepository { IReadOnlyList<SalesOrder> List(); void Add(SalesOrder item); void Update(SalesOrder item); }
