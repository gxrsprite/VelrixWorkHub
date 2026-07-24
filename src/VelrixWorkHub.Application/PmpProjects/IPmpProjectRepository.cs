using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.PmpProjects;
public interface IPmpProjectRepository
{
    IReadOnlyList<PmpProject> List();
    void Add(PmpProject item);
    void Update(PmpProject item);
    void Remove(Guid id);
}
