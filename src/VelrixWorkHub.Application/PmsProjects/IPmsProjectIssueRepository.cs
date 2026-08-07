using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.PmsProjects;
public interface IPmsProjectIssueRepository
{
    IReadOnlyList<PmsProjectIssue> List(Guid? projectId = null);
    void Add(PmsProjectIssue item);
    void Update(PmsProjectIssue item);
    void Remove(Guid id);
}
