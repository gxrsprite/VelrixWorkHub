using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Tasks;

public interface IWorkTaskRepository
{
    IReadOnlyList<WorkTask> List();
    void Add(WorkTask task);
    void Update(WorkTask task);
    void Remove(Guid taskId);
}
