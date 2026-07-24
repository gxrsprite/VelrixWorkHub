using FreeSql;
using VelrixWorkHub.Application.Employees;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Employees;

public sealed class FreeSqlOaEmployeeProfileRepository(IFreeSql fsql) : IOaEmployeeProfileRepository
{
    public IReadOnlyList<OaEmployeeProfile> List() =>
        fsql.Select<OaEmployeeProfileRecord>()
            .ToList()
            .Select(ToDomain)
            .ToArray();

    public OaEmployeeProfile? Get(Guid userId)
    {
        var record = fsql.Select<OaEmployeeProfileRecord>().Where(item => item.UserId == userId).First();
        return record is null ? null : ToDomain(record);
    }

    public void Add(OaEmployeeProfile profile) => fsql.Insert(ToRecord(profile)).ExecuteAffrows();

    public void Update(OaEmployeeProfile profile)
    {
        var rows = fsql.Update<OaEmployeeProfileRecord>()
            .Set(item => item.EmployeeNo, profile.EmployeeNo)
            .Set(item => item.Phone, profile.Phone)
            .Set(item => item.Email, profile.Email)
            .Set(item => item.WeComUserId, profile.WeComUserId)
            .Set(item => item.DingTalkUserId, profile.DingTalkUserId)
            .Set(item => item.PositionTitle, profile.PositionTitle)
            .Set(item => item.HireDate, profile.HireDate?.ToDateTime(TimeOnly.MinValue))
            .Set(item => item.Status, profile.Status)
            .Set(item => item.OtherInfo, profile.OtherInfo)
            .Where(item => item.UserId == profile.UserId)
            .ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("员工档案不存在或已被删除。");
    }

    private static OaEmployeeProfile ToDomain(OaEmployeeProfileRecord record) =>
        new(
            record.UserId,
            record.EmployeeNo,
            record.Phone,
            record.Email,
            record.PositionTitle,
            record.HireDate is DateTime hireDate ? DateOnly.FromDateTime(hireDate) : null,
            record.Status,
            record.OtherInfo,
            record.WeComUserId,
            record.DingTalkUserId);

    private static OaEmployeeProfileRecord ToRecord(OaEmployeeProfile profile) => new()
    {
        UserId = profile.UserId,
        EmployeeNo = profile.EmployeeNo,
        Phone = profile.Phone,
        Email = profile.Email,
        WeComUserId = profile.WeComUserId,
        DingTalkUserId = profile.DingTalkUserId,
        PositionTitle = profile.PositionTitle,
        HireDate = profile.HireDate?.ToDateTime(TimeOnly.MinValue),
        Status = profile.Status,
        OtherInfo = profile.OtherInfo
    };
}
