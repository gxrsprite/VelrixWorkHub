using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmsProjects;

public interface IPmsProjectStatusHistoryRepository
{
    IReadOnlyList<PmsProjectStatusHistory> List(Guid projectId);
    void Add(PmsProjectStatusHistory history);
}
