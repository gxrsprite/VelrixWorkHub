using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmsProjects;

public interface IPmsWbsTaskRepository
{
    IReadOnlyList<PmsWbsTask> List(Guid? projectId = null);
    void Add(PmsWbsTask item);
    void Update(PmsWbsTask item);
    void Remove(Guid id);
}
