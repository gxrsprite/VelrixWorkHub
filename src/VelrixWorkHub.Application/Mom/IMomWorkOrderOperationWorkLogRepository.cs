using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomWorkOrderOperationWorkLogRepository
{
    IReadOnlyList<MomWorkOrderOperationWorkLog> List();
    void Add(MomWorkOrderOperationWorkLog item);
}
