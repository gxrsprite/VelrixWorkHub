using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomServiceWorkOrderPartConsumptionRepository
{
    IReadOnlyList<MomServiceWorkOrderPartConsumption> List(Guid? serviceWorkOrderId = null);
    void Add(MomServiceWorkOrderPartConsumption item);
}
