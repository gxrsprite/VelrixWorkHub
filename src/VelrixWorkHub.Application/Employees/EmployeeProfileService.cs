using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Employees;

public interface IOaEmployeeProfileRepository
{
    IReadOnlyList<OaEmployeeProfile> List();
    OaEmployeeProfile? Get(Guid userId);
    void Add(OaEmployeeProfile profile);
    void Update(OaEmployeeProfile profile);
}

public sealed class EmployeeProfileService(IOaEmployeeProfileRepository repository)
{
    public IReadOnlyList<OaEmployeeProfile> List() => repository.List();

    public OaEmployeeProfile? Get(Guid userId) => userId == Guid.Empty ? null : repository.Get(userId);

    public OaEmployeeProfile Save(
        Guid userId,
        string? employeeNo,
        string? phone,
        string? email,
        string? positionTitle,
        DateOnly? hireDate,
        OaEmploymentStatus status,
        string? otherInfo,
        string actor,
        bool canEdit,
        string? weComUserId = null,
        string? dingTalkUserId = null)
    {
        if (!canEdit) throw new UnauthorizedAccessException("当前用户没有编辑员工档案的权限。");
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("操作者不能为空。", nameof(actor));

        var profile = repository.Get(userId);
        if (profile is null)
        {
            profile = new OaEmployeeProfile(userId, employeeNo, phone, email, positionTitle, hireDate, status, otherInfo, weComUserId, dingTalkUserId);
            repository.Add(profile);
        }
        else
        {
            profile.Edit(employeeNo, phone, email, positionTitle, hireDate, status, otherInfo, weComUserId, dingTalkUserId);
            repository.Update(profile);
        }

        return profile;
    }
}
