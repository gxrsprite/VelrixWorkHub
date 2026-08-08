using VelrixWorkHub.Application.Employees;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

/// <summary>
/// 组合平台启用账号与 OA 员工档案；历史上没有档案的启用账号按兼容规则视为可选员工。
/// 一旦存在档案，则只有在职状态可以登记 MOM 工时。
/// </summary>
public sealed class FreeSqlMomOperatorResolver(
    IEmployeeDirectoryRepository directoryRepository,
    IOaEmployeeProfileRepository profileRepository) : IMomOperatorResolver
{
    public IReadOnlyList<MomOperator> ListActive()
    {
        var profiles = profileRepository.List().ToDictionary(x => x.UserId);
        return directoryRepository.List()
            .Where(x => x.IsEnabled && (!profiles.TryGetValue(x.UserId, out var profile) || profile.Status == OaEmploymentStatus.Employed))
            .Where(x => x.UserId != Guid.Empty && !string.IsNullOrWhiteSpace(x.Username))
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.Username)
            .Select(x => new MomOperator(x.UserId, x.Username.Trim(), string.IsNullOrWhiteSpace(x.DisplayName) ? x.Username.Trim() : x.DisplayName.Trim()))
            .ToArray();
    }

    public MomOperator? GetActive(Guid userId) => userId == Guid.Empty ? null : ListActive().FirstOrDefault(x => x.UserId == userId);
}
