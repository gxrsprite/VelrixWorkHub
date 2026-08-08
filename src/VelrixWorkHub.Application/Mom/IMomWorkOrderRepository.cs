using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomWorkOrderRepository
{
    IReadOnlyList<MomWorkOrder> List();
    void Add(MomWorkOrder item);
    void Update(MomWorkOrder item);
}
