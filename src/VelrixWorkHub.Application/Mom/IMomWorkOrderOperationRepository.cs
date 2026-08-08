using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomWorkOrderOperationRepository
{
    IReadOnlyList<MomWorkOrderOperation> List();
    void Add(MomWorkOrderOperation item);
    void Update(MomWorkOrderOperation item);
}
