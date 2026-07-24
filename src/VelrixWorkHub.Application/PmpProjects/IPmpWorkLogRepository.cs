using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.PmpProjects;
public interface IPmpWorkLogRepository
{
    IReadOnlyList<PmpWorkLog> List(Guid? projectId = null);
    void Add(PmpWorkLog item);
    void Update(PmpWorkLog item);
    void Remove(Guid id);
}
