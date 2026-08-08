using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomMaterialPlanningLineRepository
{
    IReadOnlyList<MomMaterialPlanningLine> List();
    void Add(MomMaterialPlanningLine item);
}
