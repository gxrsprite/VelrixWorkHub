using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomManufacturingComponentRepository
{
    IReadOnlyList<MomManufacturingComponent> List();
    void Add(MomManufacturingComponent item);
    void Update(MomManufacturingComponent item);
    void Remove(Guid id);
}
