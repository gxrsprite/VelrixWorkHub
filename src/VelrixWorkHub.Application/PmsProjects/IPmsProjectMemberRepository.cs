using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmsProjects;

public interface IPmsProjectMemberRepository
{
    IReadOnlyList<PmsProjectMember> List(Guid? projectId = null);
    void Add(PmsProjectMember item);
    void Update(PmsProjectMember item);
    void Remove(Guid id);
}
