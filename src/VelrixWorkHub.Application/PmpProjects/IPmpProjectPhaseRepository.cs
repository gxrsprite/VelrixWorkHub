using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmpProjects;

public interface IPmpProjectPhaseRepository
{
    IReadOnlyList<PmpProjectPhase> List(Guid? projectId = null);
    void Add(PmpProjectPhase item);
    void Update(PmpProjectPhase item);
    void Remove(Guid id);
}
