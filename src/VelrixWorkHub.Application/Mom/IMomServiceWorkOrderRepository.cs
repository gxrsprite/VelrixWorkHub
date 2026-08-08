using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomServiceWorkOrderRepository
{
    IReadOnlyList<MomServiceWorkOrder> List(Guid? equipmentId = null);
    MomServiceWorkOrder? Get(Guid id);
    void Add(MomServiceWorkOrder item);
    void Update(MomServiceWorkOrder item);
}

public interface IMomServiceWorkOrderHistoryRepository
{
    IReadOnlyList<MomServiceWorkOrderHistory> List(Guid workOrderId);
    void Add(MomServiceWorkOrderHistory item);
}
