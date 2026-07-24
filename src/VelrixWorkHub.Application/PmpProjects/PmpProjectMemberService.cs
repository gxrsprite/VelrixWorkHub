using VelrixWorkHub.Domain;
using VelrixWorkHub.Application.Employees;

namespace VelrixWorkHub.Application.PmpProjects;

public sealed class PmpProjectMemberService(IPmpProjectMemberRepository repository, IPmpProjectRepository projectRepository, EmployeeDirectoryService? directory = null)
{
    public IReadOnlyList<PmpProjectMember> List(Guid? projectId = null, string? keyword = null)
    {
        var query = repository.List(projectId).AsEnumerable(); var text = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(text)) query = query.Where(x => x.MemberName.Contains(text, StringComparison.OrdinalIgnoreCase) || x.RoleName.Contains(text, StringComparison.OrdinalIgnoreCase));
        return query.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.MemberName).ToArray();
    }

    public PmpProjectMember Create(Guid projectId, string memberName, string roleName, bool isPrimary, string? departmentName = null)
    {
        EnsureProject(projectId); var item = new PmpProjectMember(projectId, memberName, roleName, isPrimary, departmentName); EnsureUnique(item); repository.Add(item); return item;
    }

    public PmpProjectMember CreateForPerson(Guid projectId, Guid userId, string roleName, bool isPrimary)
    {
        EnsureProject(projectId);
        var person = ResolveEnabledPerson(userId);
        var item = new PmpProjectMember(projectId, person.DisplayName, roleName, isPrimary, person.OrganizationName, person.UserId);
        EnsureUnique(item);
        repository.Add(item);
        return item;
    }

    public void Edit(PmpProjectMember item, string memberName, string roleName, string? departmentName = null)
    {
        EnsureUnique(item.ProjectId, item.Id, item.UserId, memberName);
        item.Edit(item.ProjectId, memberName, roleName, departmentName, item.UserId);
        repository.Update(item);
    }

    public void EditForPerson(PmpProjectMember item, Guid userId, string roleName)
    {
        var person = ResolveEnabledPerson(userId);
        EnsureUnique(item.ProjectId, item.Id, person.UserId, person.DisplayName);
        item.Edit(item.ProjectId, person.DisplayName, roleName, person.OrganizationName, person.UserId);
        repository.Update(item);
    }
    public void SetPrimary(PmpProjectMember item, bool value)
    {
        foreach (var other in repository.List(item.ProjectId).Where(x => x.Id != item.Id && value && x.IsPrimary)) { other.SetPrimary(false); repository.Update(other); }
        item.SetPrimary(value); repository.Update(item);
    }
    public void Remove(PmpProjectMember item) => repository.Remove(item.Id);
    private void EnsureProject(Guid id) { if (!projectRepository.List().Any(x => x.Id == id)) throw new InvalidOperationException("关联项目不存在。"); }
    private EmployeeDirectoryEntry ResolveEnabledPerson(Guid userId)
    {
        if (userId == Guid.Empty) throw new ArgumentException("请选择项目成员。", nameof(userId));
        var person = directory?.List(status: EmployeeDirectoryStatus.Enabled).FirstOrDefault(x => x.UserId == userId);
        return person ?? throw new ArgumentException("项目成员不存在或已停用。", nameof(userId));
    }
    private void EnsureUnique(PmpProjectMember item) => EnsureUnique(item.ProjectId, item.Id, item.UserId, item.MemberName);
    private void EnsureUnique(Guid projectId, Guid itemId, Guid? userId, string memberName)
    {
        var duplicate = userId is Guid id
            ? repository.List(projectId).Any(x => x.Id != itemId && x.UserId == id)
            : repository.List(projectId).Any(x => x.Id != itemId && x.MemberName.Equals(memberName?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (duplicate) throw new InvalidOperationException("同一项目下成员不能重复绑定。");
    }
}
