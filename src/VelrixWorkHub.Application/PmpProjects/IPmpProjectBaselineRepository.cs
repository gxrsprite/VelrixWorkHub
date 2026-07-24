using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmpProjects;

public interface IPmpProjectBaselineRepository
{
    IReadOnlyList<PmpProjectBaseline> List(Guid? projectId = null);
    int NextVersion(Guid projectId);
    void Add(PmpProjectBaseline item);
}
