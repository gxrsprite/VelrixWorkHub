using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomMaterialConsumptionRepository
{
    IReadOnlyList<MomMaterialConsumption> List();
    void Add(MomMaterialConsumption item);
}
