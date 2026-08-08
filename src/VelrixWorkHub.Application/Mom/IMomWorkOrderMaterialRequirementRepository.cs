using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomWorkOrderMaterialRequirementRepository
{
    IReadOnlyList<MomWorkOrderMaterialRequirement> List();
    void Add(MomWorkOrderMaterialRequirement item);
    void Update(MomWorkOrderMaterialRequirement item);
}
