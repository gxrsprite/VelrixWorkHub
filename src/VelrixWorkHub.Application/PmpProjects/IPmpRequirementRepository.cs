using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmpProjects;

public interface IPmpRequirementRepository
{
    IReadOnlyList<PmpRequirement> List(Guid? projectId = null);
    void Add(PmpRequirement item);
    void Update(PmpRequirement item);
    void Remove(Guid id);
}
