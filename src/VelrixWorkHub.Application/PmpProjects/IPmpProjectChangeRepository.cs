using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.PmpProjects;
public interface IPmpProjectChangeRepository
{
    IReadOnlyList<PmpProjectChange> List(Guid? projectId = null);
    void Add(PmpProjectChange item);
    void Update(PmpProjectChange item);
}
