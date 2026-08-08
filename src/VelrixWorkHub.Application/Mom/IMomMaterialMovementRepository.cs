using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomMaterialMovementRepository
{
    IReadOnlyList<MomMaterialMovement> List();
    void Add(MomMaterialMovement item);
}
