using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmsProjects;

public interface IPmsProjectBaselineRepository
{
    IReadOnlyList<PmsProjectBaseline> List(Guid? projectId = null);
    int NextVersion(Guid projectId);
    void Add(PmsProjectBaseline item);
}
