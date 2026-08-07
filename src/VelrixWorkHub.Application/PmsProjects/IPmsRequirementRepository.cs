using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmsProjects;

public interface IPmsRequirementRepository
{
    IReadOnlyList<PmsRequirement> List(Guid? projectId = null);
    void Add(PmsRequirement item);
    void Update(PmsRequirement item);
    void Remove(Guid id);
}
