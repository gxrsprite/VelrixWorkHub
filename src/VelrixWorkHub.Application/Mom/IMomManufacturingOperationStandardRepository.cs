using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomManufacturingOperationStandardRepository
{
    IReadOnlyList<MomManufacturingOperationStandard> List();
    void Add(MomManufacturingOperationStandard item);
    void Update(MomManufacturingOperationStandard item);
    void Remove(Guid id);
}
