using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomWorkOrderOperationReportRepository
{
    IReadOnlyList<MomWorkOrderOperationReport> List();
    void Add(MomWorkOrderOperationReport item);
}
