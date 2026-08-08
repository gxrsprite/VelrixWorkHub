using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomManufacturingVersionRepository
{
    IReadOnlyList<MomManufacturingVersion> List();
    void Add(MomManufacturingVersion item);
    void Update(MomManufacturingVersion item);
}
