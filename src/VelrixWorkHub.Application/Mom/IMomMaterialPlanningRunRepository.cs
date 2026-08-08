using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomMaterialPlanningRunRepository
{
    IReadOnlyList<MomMaterialPlanningRun> List();
    void Add(MomMaterialPlanningRun item);
    void Update(MomMaterialPlanningRun item);
}
