using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.PmsProjects;
public interface IPmsWorkLogRepository
{
    IReadOnlyList<PmsWorkLog> List(Guid? projectId = null);
    void Add(PmsWorkLog item);
    void Update(PmsWorkLog item);
    void Remove(Guid id);
}
