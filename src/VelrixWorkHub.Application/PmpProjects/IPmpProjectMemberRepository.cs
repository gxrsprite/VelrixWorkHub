using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmpProjects;

public interface IPmpProjectMemberRepository
{
    IReadOnlyList<PmpProjectMember> List(Guid? projectId = null);
    void Add(PmpProjectMember item);
    void Update(PmpProjectMember item);
    void Remove(Guid id);
}
