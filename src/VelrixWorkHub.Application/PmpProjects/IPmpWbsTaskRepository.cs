using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmpProjects;

public interface IPmpWbsTaskRepository
{
    IReadOnlyList<PmpWbsTask> List(Guid? projectId = null);
    void Add(PmpWbsTask item);
    void Update(PmpWbsTask item);
    void Remove(Guid id);
}
