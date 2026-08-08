using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomMaterialConsumptionReversalRepository
{
    IReadOnlyList<MomMaterialConsumptionReversal> List();
    void Add(MomMaterialConsumptionReversal item);
}
