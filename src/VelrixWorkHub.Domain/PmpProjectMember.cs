namespace VelrixWorkHub.Domain;

public sealed class PmpProjectMember
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ProjectId { get; private set; }
    public Guid? UserId { get; private set; }
    public string MemberName { get; private set; } = string.Empty;
    public string RoleName { get; private set; } = string.Empty;
    public string? DepartmentName { get; private set; }
    public bool IsPrimary { get; private set; }

    public PmpProjectMember(Guid projectId, string memberName, string roleName, bool isPrimary = false, string? departmentName = null, Guid? userId = null)
    { Edit(projectId, memberName, roleName, departmentName, userId); IsPrimary = isPrimary; }

    public void Edit(Guid projectId, string memberName, string roleName)
        => Edit(projectId, memberName, roleName, DepartmentName, UserId);

    public void Edit(Guid projectId, string memberName, string roleName, string? departmentName, Guid? userId = null)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("必须关联项目。", nameof(projectId));
        if (string.IsNullOrWhiteSpace(memberName)) throw new ArgumentException("成员姓名不能为空。", nameof(memberName));
        if (string.IsNullOrWhiteSpace(roleName)) throw new ArgumentException("成员角色不能为空。", nameof(roleName));
        ProjectId = projectId; UserId = userId; MemberName = memberName.Trim(); RoleName = roleName.Trim(); DepartmentName = string.IsNullOrWhiteSpace(departmentName) ? null : departmentName.Trim();
    }

    public void SetPrimary(bool value) => IsPrimary = value;
}
