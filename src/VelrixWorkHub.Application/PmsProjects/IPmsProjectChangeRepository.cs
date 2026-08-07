using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.PmsProjects;
public interface IPmsProjectChangeRepository
{
    IReadOnlyList<PmsProjectChange> List(Guid? projectId = null);
    void Add(PmsProjectChange item);
    void Update(PmsProjectChange item);
}
