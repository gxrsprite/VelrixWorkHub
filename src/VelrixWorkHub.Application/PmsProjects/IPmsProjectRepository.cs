using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.PmsProjects;
public interface IPmsProjectRepository
{
    IReadOnlyList<PmsProject> List();
    void Add(PmsProject item);
    void Update(PmsProject item);
    void Remove(Guid id);
}
