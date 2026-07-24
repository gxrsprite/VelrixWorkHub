using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmpProjects;

public interface IPmpProjectStatusHistoryRepository
{
    IReadOnlyList<PmpProjectStatusHistory> List(Guid projectId);
    void Add(PmpProjectStatusHistory history);
}
