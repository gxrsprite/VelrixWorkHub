using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomMaterialConsumptionAllocationRepository
{
    IReadOnlyList<MomMaterialConsumptionAllocation> List();
    void Add(MomMaterialConsumptionAllocation item);
}
