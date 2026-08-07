using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmsProjects;

public interface IPmsProjectPhaseRepository
{
    IReadOnlyList<PmsProjectPhase> List(Guid? projectId = null);
    void Add(PmsProjectPhase item);
    void Update(PmsProjectPhase item);
    void Remove(Guid id);
}
