using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.PmpProjects;
public interface IPmpProjectIssueRepository
{
    IReadOnlyList<PmpProjectIssue> List(Guid? projectId = null);
    void Add(PmpProjectIssue item);
    void Update(PmpProjectIssue item);
    void Remove(Guid id);
}
